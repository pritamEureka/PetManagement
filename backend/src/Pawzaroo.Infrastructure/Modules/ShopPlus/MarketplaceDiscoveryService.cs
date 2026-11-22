using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.ShopPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

public class MarketplaceDiscoveryService : IMarketplaceDiscoveryService
{
    private const string ProductVersionKey = "marketplace:products:version"; // bumped by MarketplaceCache on product writes
    private static readonly TimeSpan TermsTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FacetsTtl = TimeSpan.FromSeconds(60);
    private const int RelevanceCandidates = 300;
    private const int MaxSavedSearches = 20;
    private const int MaxRecentlyViewed = 50;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Ranking weights (renormalised over the components that apply to a request).
    private const double WText = 0.40, WRating = 0.20, WInventory = 0.15, WDistance = 0.15, WPet = 0.10;

    private static readonly Dictionary<AnimalType, string[]> SpeciesKeywords = new()
    {
        [AnimalType.Dog] = new[] { "dog", "puppy", "canine" },
        [AnimalType.Cat] = new[] { "cat", "kitten", "feline" },
        [AnimalType.Bird] = new[] { "bird", "parrot", "avian" },
        [AnimalType.Rabbit] = new[] { "rabbit", "bunny" },
        [AnimalType.Fish] = new[] { "fish", "aquarium" },
        [AnimalType.Reptile] = new[] { "reptile", "lizard", "snake" },
        [AnimalType.Cow] = new[] { "cow", "cattle" },
        [AnimalType.Goat] = new[] { "goat" },
        [AnimalType.Sheep] = new[] { "sheep" },
        [AnimalType.Horse] = new[] { "horse", "equine" },
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IRedisCacheService _cache;

    public MarketplaceDiscoveryService(ApplicationDbContext db, ICurrentUserService current, IRedisCacheService cache)
    {
        _db = db;
        _current = current;
        _cache = cache;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    // =====================================================================================
    // Search + facets + explainable ranking
    // =====================================================================================

    public async Task<FacetSearchResult> SearchAsync(FacetSearchRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 60);
        var tokens = TextSimilarity.Tokenize(request.Q);
        string? didYouMean = null;
        var effectiveQuery = request.Q;

        var baseQ = DiscoveryFilters.PublicProducts(_db);
        var total = await DiscoveryFilters.Apply(baseQ, request, tokens).CountAsync(ct);

        if (total == 0 && tokens.Length > 0)
        {
            var terms = await GetTermsAsync(ct);
            var corrected = tokens.Select(t => TextSimilarity.BestCorrection(t, terms) ?? t).ToArray();
            if (!corrected.SequenceEqual(tokens))
            {
                var total2 = await DiscoveryFilters.Apply(baseQ, request, corrected).CountAsync(ct);
                if (total2 > 0)
                {
                    tokens = corrected;
                    total = total2;
                    didYouMean = string.Join(' ', corrected);
                    effectiveQuery = didYouMean;
                }
            }
        }

        var filtered = DiscoveryFilters.Apply(baseQ, request, tokens);
        var sort = (request.Sort ?? "relevance").ToLowerInvariant();
        var ctx = await BuildRankingContextAsync(request, tokens, ct);

        List<RankedProductDto> items;
        if (sort == "relevance")
        {
            var candidates = await filtered
                .OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt)
                .Take(RelevanceCandidates).Select(DiscoveryFilters.Summary).ToListAsync(ct);
            var ranked = await ScoreAsync(candidates, ctx, ct);
            items = ranked.OrderByDescending(r => r.Score).Skip((page - 1) * size).Take(size).ToList();
        }
        else
        {
            var ordered = sort switch
            {
                "price_asc" => filtered.OrderBy(p => p.DiscountPrice ?? p.Price),
                "price_desc" => filtered.OrderByDescending(p => p.DiscountPrice ?? p.Price),
                "rating_desc" => filtered.OrderByDescending(p => p.RatingAverage),
                _ => filtered.OrderByDescending(p => p.CreatedAt),
            };
            var pageItems = await ordered.Skip((page - 1) * size).Take(size).Select(DiscoveryFilters.Summary).ToListAsync(ct);
            items = await ScoreAsync(pageItems, ctx, ct);
        }

        var facets = await FacetsAsync(request, tokens, ct);
        return new FacetSearchResult(items, total, page, size, facets, didYouMean, effectiveQuery);
    }

    private sealed record RankingContext(
        string[] Tokens, string? Phrase, double? Lat, double? Lng, string[] PetKeywords);

    private async Task<RankingContext> BuildRankingContextAsync(FacetSearchRequest r, string[] tokens, CancellationToken ct)
    {
        var keywords = Array.Empty<string>();
        if (_current.UserId is { } uid)
        {
            var species = await _db.Set<Pet>().AsNoTracking().Where(p => p.OwnerId == uid)
                .Select(p => p.AnimalType).Distinct().ToListAsync(ct);
            keywords = species.Where(SpeciesKeywords.ContainsKey).SelectMany(s => SpeciesKeywords[s]).Distinct().ToArray();
        }
        var hasGeo = r.Lat is >= -90 and <= 90 && r.Lng is >= -180 and <= 180;
        return new RankingContext(tokens, tokens.Length > 0 ? string.Join(' ', tokens) : null,
            hasGeo ? r.Lat : null, hasGeo ? r.Lng : null, keywords);
    }

    private async Task<List<RankedProductDto>> ScoreAsync(List<ProductSummaryDto> items, RankingContext ctx, CancellationToken ct)
    {
        var locations = new Dictionary<Guid, StoreLocation>();
        if (ctx.Lat is not null && items.Count > 0)
        {
            var storeIds = items.Select(i => i.StoreId).Distinct().ToList();
            locations = await _db.Set<StoreLocation>().AsNoTracking().Where(l => storeIds.Contains(l.StoreId))
                .ToDictionaryAsync(l => l.StoreId, ct);
        }
        return items.Select(i => ScoreOne(i, ctx, locations)).ToList();
    }

    private static RankedProductDto ScoreOne(ProductSummaryDto p, RankingContext ctx, Dictionary<Guid, StoreLocation> locations)
    {
        var parts = new List<(string Name, double Value, double Weight, string? Note)>();
        double? distanceKm = null;

        if (ctx.Tokens.Length > 0)
        {
            var text = TextRelevance(p, ctx);
            parts.Add(("text relevance", text.Score, WText, text.Note));
        }

        var prior = 3.5; var priorWeight = 5.0;
        var bayes = ((double)p.RatingAverage * p.RatingCount + prior * priorWeight) / (p.RatingCount + priorWeight) / 5.0;
        parts.Add(("rating", Math.Clamp(bayes, 0, 1), WRating, $"{p.RatingAverage:0.0} from {p.RatingCount} reviews"));

        var inv = p.StockQuantity <= 0 ? 0.0 : 0.5 + 0.5 * Math.Min(1.0, p.StockQuantity / 20.0);
        parts.Add(("inventory", inv, WInventory, p.StockQuantity <= 0 ? "out of stock" : $"{p.StockQuantity} in stock"));

        if (ctx.Lat is { } lat && ctx.Lng is { } lng)
        {
            if (locations.TryGetValue(p.StoreId, out var loc))
            {
                distanceKm = DiscoveryFilters.HaversineKm(lat, lng, loc.Latitude, loc.Longitude);
                parts.Add(("distance", Math.Exp(-distanceKm.Value / 25.0), WDistance, $"{distanceKm.Value:0.#} km away"));
            }
            else parts.Add(("distance", 0.3, WDistance, "store location unknown"));
        }

        if (ctx.PetKeywords.Length > 0)
        {
            var hay = $"{p.Name} {p.CategoryName} {p.BrandName}".ToLowerInvariant();
            var hit = ctx.PetKeywords.FirstOrDefault(k => hay.Contains(k));
            parts.Add(("pet match", hit is null ? 0.0 : 1.0, WPet, hit is null ? "no match for your pets" : $"matches your pet ({hit})"));
        }

        var sumW = parts.Sum(x => x.Weight);
        var comps = parts.Select(x =>
        {
            var w = x.Weight / sumW;
            return new ScoreComponentDto(x.Name, Math.Round(x.Value, 4), Math.Round(w, 4), Math.Round(w * x.Value, 4), x.Note);
        }).ToList();
        var total = Math.Round(comps.Sum(c => c.Contribution), 4);
        return new RankedProductDto(p, total, new ScoreBreakdownDto(total, comps, distanceKm is null ? null : Math.Round(distanceKm.Value, 2)));
    }

    private static (double Score, string? Note) TextRelevance(ProductSummaryDto p, RankingContext ctx)
    {
        var nameTokens = TextSimilarity.Tokenize(p.Name);
        var name = p.Name.ToLowerInvariant();
        var sku = p.Sku.ToLowerInvariant();
        var brand = (p.BrandName ?? "").ToLowerInvariant();
        var cat = (p.CategoryName ?? "").ToLowerInvariant();

        double sum = 0;
        string? note = null;
        foreach (var t in ctx.Tokens)
        {
            double best; string how;
            if (nameTokens.Contains(t)) { best = 1.0; how = "exact word in name"; }
            else if (nameTokens.Any(n => n.StartsWith(t))) { best = 0.85; how = "name prefix"; }
            else if (name.Contains(t)) { best = 0.7; how = "name contains"; }
            else if (sku.Contains(t)) { best = 0.8; how = "SKU match"; }
            else if (brand.Contains(t) || cat.Contains(t)) { best = 0.5; how = "brand/category match"; }
            else
            {
                var fuzzy = nameTokens.Length == 0 ? 0 : nameTokens.Max(n => TextSimilarity.Trigram(t, n));
                best = fuzzy * 0.6; how = "fuzzy name match";
            }
            note ??= how;
            sum += best;
        }
        var score = sum / ctx.Tokens.Length;
        if (ctx.Phrase is not null && ctx.Tokens.Length > 1 && name.Contains(ctx.Phrase)) { score += 0.1; note = "phrase in name"; }
        return (Math.Min(1.0, score), note);
    }

    // ---- facets ------------------------------------------------------------------------

    private async Task<FacetsDto> FacetsAsync(FacetSearchRequest r, string[] tokens, CancellationToken ct)
    {
        var version = await _cache.GetAsync<long?>(ProductVersionKey, ct) ?? 0;
        var keyJson = JsonSerializer.Serialize(new { tokens, r.CategoryId, r.BrandId, r.StoreId, r.MinPrice, r.MaxPrice, r.MinRating, r.InStockOnly });
        var key = $"shopplus:facets:{version}:{Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(keyJson)))}";
        var cached = await _cache.GetAsync<FacetsDto>(key, ct);
        if (cached is not null) return cached;

        var baseQ = DiscoveryFilters.PublicProducts(_db);

        var cats = await DiscoveryFilters.Apply(baseQ, r, tokens, "category").Where(p => p.CategoryId != null)
            .GroupBy(p => new { p.CategoryId, Name = p.Category != null ? p.Category.Name : "" })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(20).ToListAsync(ct);
        var brands = await DiscoveryFilters.Apply(baseQ, r, tokens, "brand").Where(p => p.BrandId != null)
            .GroupBy(p => new { p.BrandId, Name = p.Brand != null ? p.Brand.Name : "" })
            .Select(g => new { g.Key.BrandId, g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(20).ToListAsync(ct);
        var stores = await DiscoveryFilters.Apply(baseQ, r, tokens, "store")
            .GroupBy(p => new { p.StoreId, p.Store.Name })
            .Select(g => new { g.Key.StoreId, g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(20).ToListAsync(ct);

        var prices = await DiscoveryFilters.Apply(baseQ, r, tokens, "price")
            .Select(p => p.DiscountPrice ?? p.Price).Take(5000).ToListAsync(ct);
        var ratings = await DiscoveryFilters.Apply(baseQ, r, tokens, "rating")
            .Select(p => p.RatingAverage).Take(5000).ToListAsync(ct);
        var inStock = await DiscoveryFilters.Apply(baseQ, r, tokens, "stock").CountAsync(p => p.StockQuantity > 0, ct);

        var facets = new FacetsDto(
            cats.Select(c => new FacetBucket(c.CategoryId!.Value.ToString(), c.Name, c.Count, c.CategoryId == r.CategoryId)).ToList(),
            brands.Select(c => new FacetBucket(c.BrandId!.Value.ToString(), c.Name, c.Count, c.BrandId == r.BrandId)).ToList(),
            stores.Select(c => new FacetBucket(c.StoreId.ToString(), c.Name, c.Count, c.StoreId == r.StoreId)).ToList(),
            PriceBuckets(prices, r),
            new[] { 4, 3, 2, 1 }.Select(t => new FacetBucket(t.ToString(), $"{t}+ stars",
                ratings.Count(x => x >= t), r.MinRating == t)).ToList(),
            new[] { new FacetBucket("in", "In stock", inStock, r.InStockOnly == true) });

        await _cache.SetAsync(key, facets, FacetsTtl, ct);
        return facets;
    }

    private static List<FacetBucket> PriceBuckets(List<decimal> prices, FacetSearchRequest r)
    {
        if (prices.Count == 0) return new();
        var min = prices.Min();
        var max = prices.Max();
        if (min == max) return new() { new FacetBucket($"{Math.Floor(min)}-{Math.Ceiling(max)}", $"{min:0.##}", prices.Count) };

        var raw = (double)(max - min) / 5.0;
        var pow = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = (decimal)(Math.Ceiling(raw / pow) * pow);
        if (step <= 0) step = 1;
        var lo = Math.Floor(min / step) * step;

        var list = new List<FacetBucket>();
        for (var lower = lo; lower <= max; lower += step)
        {
            var upper = lower + step;
            var count = prices.Count(p => p >= lower && p < upper);
            if (count == 0) continue;
            var selected = r.MinPrice == lower && r.MaxPrice == upper;
            list.Add(new FacetBucket($"{lower:0.##}-{upper:0.##}", $"{lower:0.##} - {upper:0.##}", count, selected));
        }
        return list;
    }

    // =====================================================================================
    // Suggestions (prefix + typo tolerance over a cached term list)
    // =====================================================================================

    private async Task<Dictionary<string, int>> GetTermsAsync(CancellationToken ct)
    {
        var version = await _cache.GetAsync<long?>(ProductVersionKey, ct) ?? 0;
        var key = $"shopplus:terms:{version}";
        var cached = await _cache.GetAsync<Dictionary<string, int>>(key, ct);
        if (cached is not null) return cached;

        var rows = await DiscoveryFilters.PublicProducts(_db)
            .Select(p => new { p.Name, Brand = p.Brand != null ? p.Brand.Name : null, Category = p.Category != null ? p.Category.Name : null })
            .Take(5000).ToListAsync(ct);

        var terms = new Dictionary<string, int>();
        void Add(string? s, int weight)
        {
            foreach (var t in TextSimilarity.Tokenize(s))
                if (t.Length >= 3) terms[t] = terms.GetValueOrDefault(t) + weight;
        }
        foreach (var r in rows) { Add(r.Name, 1); Add(r.Brand, 2); Add(r.Category, 2); }

        await _cache.SetAsync(key, terms, TermsTtl, ct);
        return terms;
    }

    public async Task<IReadOnlyList<SuggestionDto>> SuggestAsync(string q, int limit = 8, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 20);
        var words = TextSimilarity.Tokenize(q);
        if (words.Length == 0) return Array.Empty<SuggestionDto>();

        var prefix = words[^1];
        var lead = string.Join(' ', words.Take(words.Length - 1));
        string Compose(string term) => lead.Length == 0 ? term : $"{lead} {term}";

        var terms = await GetTermsAsync(ct);
        var results = new List<SuggestionDto>();
        var seen = new HashSet<string>();
        void Push(string text, string kind)
        {
            if (seen.Add(text.ToLowerInvariant())) results.Add(new SuggestionDto(text, kind));
        }

        foreach (var t in terms.Where(k => k.Key.StartsWith(prefix) && k.Key != prefix)
                     .OrderByDescending(k => k.Value).ThenBy(k => k.Key.Length).Take(limit))
            Push(Compose(t.Key), "term");

        var max = TextSimilarity.MaxEdits(prefix.Length);
        if (max > 0 && results.Count < limit && !terms.ContainsKey(prefix))
        {
            var fuzzy = terms
                .Select(k => new { k.Key, k.Value, D = TextSimilarity.Levenshtein(prefix, k.Key, max) })
                .Where(x => x.D <= max)
                .OrderBy(x => x.D).ThenByDescending(x => x.Value).Take(limit);
            foreach (var f in fuzzy) Push(Compose(f.Key), "correction");
        }

        if (results.Count < limit)
        {
            var like = "%" + q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var names = await DiscoveryFilters.PublicProducts(_db).Where(p => EF.Functions.ILike(p.Name, like))
                .OrderByDescending(p => p.IsFeatured).Select(p => p.Name).Take(3).ToListAsync(ct);
            foreach (var n in names) Push(n, "product");
        }
        return results.Take(limit).ToList();
    }

    // =====================================================================================
    // Saved searches
    // =====================================================================================

    public async Task<IReadOnlyList<SavedSearchDto>> ListSavedSearchesAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var rows = await _db.Set<SavedSearch>().AsNoTracking().Where(s => s.UserId == uid)
            .OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<SavedSearchDto> CreateSavedSearchAsync(CreateSavedSearchInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
            throw new ValidationException(new Dictionary<string, string[]> { ["name"] = new[] { "Name is required (max 100 chars)." } });
        if (await _db.Set<SavedSearch>().CountAsync(s => s.UserId == uid, ct) >= MaxSavedSearches)
            throw new ConflictException($"You can save up to {MaxSavedSearches} searches.");

        var query = (input.Query ?? new FacetSearchRequest()) with { Page = 1, Lat = null, Lng = null };
        var s = new SavedSearch
        {
            UserId = uid, Name = input.Name.Trim(), QueryJson = JsonSerializer.Serialize(query, Json),
            AlertEnabled = input.AlertEnabled, LastCheckedAt = DateTime.UtcNow, CreatedBy = uid
        };
        _db.Set<SavedSearch>().Add(s);
        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<SavedSearchDto> UpdateSavedSearchAsync(Guid id, UpdateSavedSearchInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var s = await _db.Set<SavedSearch>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
                ?? throw new NotFoundException("SavedSearch", id);
        if (!string.IsNullOrWhiteSpace(input.Name)) s.Name = input.Name.Trim();
        if (input.AlertEnabled is { } a)
        {
            if (a && !s.AlertEnabled) s.LastCheckedAt = DateTime.UtcNow;
            s.AlertEnabled = a;
        }
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task DeleteSavedSearchAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var s = await _db.Set<SavedSearch>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
                ?? throw new NotFoundException("SavedSearch", id);
        _db.Set<SavedSearch>().Remove(s);
        await _db.SaveChangesAsync(ct);
    }

    private static SavedSearchDto ToDto(SavedSearch s)
    {
        FacetSearchRequest q;
        try { q = JsonSerializer.Deserialize<FacetSearchRequest>(s.QueryJson, Json) ?? new FacetSearchRequest(); }
        catch (JsonException) { q = new FacetSearchRequest(); }
        return new SavedSearchDto(s.Id, s.Name, q, s.AlertEnabled, s.CreatedAt);
    }

    // =====================================================================================
    // Recently viewed
    // =====================================================================================

    public async Task RecordViewAsync(Guid productId, CancellationToken ct = default)
    {
        var uid = Uid();
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct)) throw new NotFoundException("Product", productId);

        var now = DateTime.UtcNow;
        var row = await _db.Set<RecentlyViewedItem>().FirstOrDefaultAsync(x => x.UserId == uid && x.ProductId == productId, ct);
        if (row is null) _db.Set<RecentlyViewedItem>().Add(new RecentlyViewedItem { UserId = uid, ProductId = productId, ViewedAt = now });
        else row.ViewedAt = now;
        await _db.SaveChangesAsync(ct);

        var stale = await _db.Set<RecentlyViewedItem>().Where(x => x.UserId == uid)
            .OrderByDescending(x => x.ViewedAt).Skip(MaxRecentlyViewed).ToListAsync(ct);
        if (stale.Count > 0)
        {
            _db.Set<RecentlyViewedItem>().RemoveRange(stale);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> RecentlyViewedAsync(int limit = 12, CancellationToken ct = default)
    {
        var uid = Uid();
        limit = Math.Clamp(limit, 1, MaxRecentlyViewed);
        var ids = await _db.Set<RecentlyViewedItem>().AsNoTracking().Where(x => x.UserId == uid)
            .OrderByDescending(x => x.ViewedAt).Select(x => x.ProductId).Take(limit * 2).ToListAsync(ct);
        if (ids.Count == 0) return Array.Empty<ProductSummaryDto>();

        var products = await DiscoveryFilters.PublicProducts(_db).Where(p => ids.Contains(p.Id))
            .Select(DiscoveryFilters.Summary).ToListAsync(ct);
        var byId = products.ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(i => byId[i]).Take(limit).ToList();
    }

    // =====================================================================================
    // Price watches
    // =====================================================================================

    public async Task<IReadOnlyList<PriceWatchDto>> ListPriceWatchesAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var rows = await _db.Set<PriceWatch>().AsNoTracking().Include(w => w.Product).ThenInclude(p => p.Images)
            .Where(w => w.UserId == uid).OrderByDescending(w => w.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PriceWatchDto> CreatePriceWatchAsync(CreatePriceWatchInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        if (input.TargetPrice is < 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["targetPrice"] = new[] { "Target cannot be negative." } });

        var product = await _db.Products.AsNoTracking().Include(p => p.Images)
                          .FirstOrDefaultAsync(p => p.Id == input.ProductId && p.IsActive, ct)
                      ?? throw new NotFoundException("Product", input.ProductId);
        var current = product.DiscountPrice ?? product.Price;

        var w = await _db.Set<PriceWatch>().FirstOrDefaultAsync(x => x.UserId == uid && x.ProductId == product.Id, ct);
        if (w is null)
        {
            w = new PriceWatch { UserId = uid, ProductId = product.Id, BaselinePrice = current, TargetPrice = input.TargetPrice, CreatedBy = uid };
            _db.Set<PriceWatch>().Add(w);
        }
        else
        {
            w.BaselinePrice = current;
            w.TargetPrice = input.TargetPrice;
            w.IsActive = true;
            w.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        w.Product = product;
        return ToDto(w);
    }

    public async Task DeletePriceWatchAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var w = await _db.Set<PriceWatch>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
                ?? throw new NotFoundException("PriceWatch", id);
        _db.Set<PriceWatch>().Remove(w);
        await _db.SaveChangesAsync(ct);
    }

    private static PriceWatchDto ToDto(PriceWatch w) => new(
        w.Id, w.ProductId, w.Product.Name, w.Product.Images.OrderBy(i => i.OrderIndex).Select(i => i.Url).FirstOrDefault(),
        w.BaselinePrice, w.Product.DiscountPrice ?? w.Product.Price, w.TargetPrice, w.IsActive, w.LastNotifiedAt);

    // =====================================================================================
    // Store location
    // =====================================================================================

    public async Task SetMyStoreLocationAsync(SetStoreLocationInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        if (input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180)
            throw new ValidationException(new Dictionary<string, string[]> { ["location"] = new[] { "Invalid coordinates." } });
        var storeId = await _db.Stores.AsNoTracking().Where(s => s.OwnerUserId == uid).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct)
                      ?? throw new ForbiddenException("No store registered.");

        var loc = await _db.Set<StoreLocation>().FirstOrDefaultAsync(l => l.StoreId == storeId, ct);
        if (loc is null) _db.Set<StoreLocation>().Add(new StoreLocation { StoreId = storeId, Latitude = input.Latitude, Longitude = input.Longitude });
        else { loc.Latitude = input.Latitude; loc.Longitude = input.Longitude; }
        await _db.SaveChangesAsync(ct);
    }
}
