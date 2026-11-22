using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Modules.Assistant;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Infrastructure.Modules.Pets;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.Assistant;

/// <summary>
/// Deterministic, retrieval-grounded assistant: no external LLM, no free-text generation beyond
/// templates. Every factual statement is drawn from PetKnowledge, the user's own pet records,
/// the active product catalog or approved vets, and each source is returned as a citation.
/// See <see cref="IAssistantEngine"/> for how an LLM-backed engine would replace it.
/// </summary>
public class RuleBasedAssistantEngine : IAssistantEngine
{
    public const string Disclaimer =
        "This assistant gives general information only and is not a substitute for professional veterinary advice, " +
        "diagnosis or treatment. If you are worried about your animal's health, contact a vet.";

    public const string EscalationLink = "/vets";

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","for","with","that","this","what","which","how","can","should","would","could","does","have","has",
        "are","was","were","you","your","my","our","about","any","some","good","best","need","want","find","get","give",
        "please","tell","help","there","into","from","when","why","who","its","his","her","they","them","than","then",
        "pet","pets","near","like","also","just","very","much","many","more","most","too","not","but","all","out"
    };

    private static readonly (AnimalType Type, string Pattern)[] SpeciesWords =
    {
        (AnimalType.Dog, @"\b(dogs?|pupp(y|ies)|canine)\b"),
        (AnimalType.Cat, @"\b(cats?|kittens?|feline)\b"),
        (AnimalType.Bird, @"\b(birds?|parrots?|budgies?|cockatiels?)\b"),
        (AnimalType.Rabbit, @"\b(rabbits?|bunn(y|ies))\b"),
        (AnimalType.Fish, @"\b(fish|goldfish|aquarium)\b"),
        (AnimalType.Reptile, @"\b(reptiles?|lizards?|snakes?|turtles?|tortoises?|geckos?)\b"),
        (AnimalType.Cow, @"\b(cows?|cattle|calf|calves|bull)\b"),
        (AnimalType.Goat, @"\b(goats?|kids?)\b"),
        (AnimalType.Sheep, @"\b(sheep|lambs?)\b"),
        (AnimalType.Horse, @"\b(horses?|ponn?(y|ies)|foal)\b"),
        (AnimalType.Chicken, @"\b(chickens?|hens?|poultry|roosters?)\b"),
        (AnimalType.Duck, @"\b(ducks?|ducklings?)\b"),
    };

    private static readonly (string Intent, string[] Keys)[] IntentWords =
    {
        ("vaccination", new[] { "vaccin", "shot", "booster", "rabies", "immuniz", "immunis", "jab" }),
        ("nutrition", new[] { "food", "feed", "diet", "kibble", "treat", "weight", "obese", "overweight", "nutrition", "eat" }),
        ("grooming", new[] { "groom", "bath", "brush", "nail", "shed", "coat", "flea", "tick", "fur" }),
        ("behavior", new[] { "bark", "bite", "aggress", "train", "scratch", "anxiety", "anxious", "chew", "behavio", "howl" }),
        ("vet", new[] { "vet", "doctor", "appointment", "clinic", "checkup", "check-up", "specialist", "consult" }),
        ("product", new[] { "buy", "product", "recommend", "toy", "shop", "store", "price", "collar", "leash", "bed", "cage" }),
    };

    private readonly ApplicationDbContext _db;
    private readonly IPetAccessService _access;

    public RuleBasedAssistantEngine(ApplicationDbContext db, IPetAccessService access)
    {
        _db = db;
        _access = access;
    }

    // ---------- public API ----------

    public async Task<AssistantReply> RespondAsync(AssistantRequest request, CancellationToken ct = default)
    {
        var message = request.Message.Trim();
        var lastUser = request.History.LastOrDefault(h => h.Role == "user")?.Content;
        // Short follow-ups ("and for kittens?") inherit context from the previous user turn.
        var context = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 4 && lastUser is not null
            ? lastUser + " " + message : message;

        var pet = await ResolvePetAsync(request, context, ct);
        var cityMention = Regex.Match(context, @"\bin\s+([A-Za-z][A-Za-z\- ]{2,30}?)(?:[?.!,]|$)", RegexOptions.IgnoreCase);
        var city = cityMention.Success ? cityMention.Groups[1].Value.Trim() : null;

        var triage = TriageDetector.Detect(message);
        if (triage.IsEmergency)
            return await BuildEmergencyAsync(triage, pet, city, ct);

        var species = pet?.AnimalType ?? DetectSpecies(context);
        var intent = DetectIntent(context, species, pet is not null);

        var sb = new StringBuilder();
        var cites = new List<AssistantCitation>();

        if (pet is not null)
        {
            AppendPetSummary(sb, cites, pet, await LoadVaccinationStatusAsync(pet.Id, ct));
        }

        switch (intent)
        {
            case "vaccination":
                AppendVaccination(sb, cites, species);
                break;
            case "vet":
                break; // vets appended below
            case "product":
                break; // products appended below
            default:
                AppendCareKnowledge(sb, cites, species, pet, intent);
                break;
        }

        if (intent is "vet" or "behavior" or "vaccination")
        {
            var vets = await FindVetsAsync(context, species, city, ct);
            AppendVets(sb, cites, vets, intent == "vet" ? "Vets who may be able to help" : "If you would like a professional opinion");
        }

        if (intent is "product" or "nutrition" or "grooming")
        {
            var products = await FindProductsAsync(context, species, pet, ct);
            AppendProducts(sb, cites, products);
        }

        if (cites.Count == 0)
        {
            sb.AppendLine("I could not find specific information for that in our pet knowledge base, your pet records, the store or the vet directory.");
            sb.AppendLine("Tell me which animal you are asking about (for example \"my dog\") and what you would like to know - vaccines, food, grooming, behaviour, products or finding a vet.");
        }

        return new AssistantReply(sb.ToString().TrimEnd(), cites, false, intent, Disclaimer, null);
    }

    // ---------- detection ----------

    internal static AnimalType? DetectSpecies(string text)
    {
        foreach (var (type, pattern) in SpeciesWords)
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase)) return type;
        return null;
    }

    internal static string DetectIntent(string text, AnimalType? species, bool hasPet)
    {
        var lower = text.ToLowerInvariant();
        string best = "general";
        var bestScore = 0;
        foreach (var (intent, keys) in IntentWords)
        {
            var score = keys.Count(k => lower.Contains(k));
            if (score > bestScore) { bestScore = score; best = intent; }
        }
        return best;
    }

    // ---------- retrieval: pets ----------

    private sealed record PetFacts(Guid Id, string Name, AnimalType AnimalType, string? Breed, DateTime? BirthDate,
        decimal? WeightKg, string? Allergies, AnimalSize? Size);

    private async Task<PetFacts?> ResolvePetAsync(AssistantRequest req, string context, CancellationToken ct)
    {
        if (req.PetId is { } pid)
        {
            await _access.EnsureCanReadAsync(pid, ct); // throws Forbidden/NotFound when not accessible
            return await _db.Pets.AsNoTracking().Where(p => p.Id == pid)
                .Select(p => new PetFacts(p.Id, p.Name, p.AnimalType, p.Breed, p.BirthDate, p.WeightKg, p.Allergies, p.Size))
                .FirstOrDefaultAsync(ct);
        }

        // Only pets the user OWNS are ever pulled into an answer.
        var owned = await PetAccessService.AccessiblePets(_db, req.UserId, PetAccessLevel.Owner).AsNoTracking()
            .Select(p => new PetFacts(p.Id, p.Name, p.AnimalType, p.Breed, p.BirthDate, p.WeightKg, p.Allergies, p.Size))
            .ToListAsync(ct);
        return owned.FirstOrDefault(p => p.Name.Length >= 2
            && Regex.IsMatch(context, $@"\b{Regex.Escape(p.Name)}\b", RegexOptions.IgnoreCase));
    }

    private sealed record VaccineStatus(string Name, DateTime? NextDueOn, bool Overdue);

    private async Task<List<VaccineStatus>> LoadVaccinationStatusAsync(Guid petId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.VaccinationRecords.AsNoTracking().Where(v => v.PetId == petId)
            .Select(v => new { v.VaccineName, v.AdministeredOn, v.NextDueOn }).ToListAsync(ct);
        return rows.GroupBy(r => r.VaccineName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.AdministeredOn).First())
            .Select(r => new VaccineStatus(r.VaccineName, r.NextDueOn, r.NextDueOn is { } d && d < now))
            .OrderBy(v => v.NextDueOn ?? DateTime.MaxValue).ToList();
    }

    private static void AppendPetSummary(StringBuilder sb, List<AssistantCitation> cites, PetFacts pet, List<VaccineStatus> vacc)
    {
        var stage = PetKnowledge.LifeStage(pet.AnimalType, pet.BirthDate);
        sb.Append($"About {pet.Name} (from your pet records): {pet.AnimalType}");
        if (!string.IsNullOrWhiteSpace(pet.Breed)) sb.Append($", {pet.Breed}");
        sb.Append($", life stage {stage}");
        if (pet.WeightKg is { } w) sb.Append($", {w:0.#} kg");
        sb.AppendLine(".");
        if (!string.IsNullOrWhiteSpace(pet.Allergies)) sb.AppendLine($"Recorded allergies: {pet.Allergies}.");
        var overdue = vacc.Where(v => v.Overdue).Select(v => v.Name).ToList();
        if (vacc.Count == 0) sb.AppendLine("No vaccinations are recorded yet.");
        else if (overdue.Count > 0) sb.AppendLine($"Overdue vaccinations: {string.Join(", ", overdue)}.");
        else sb.AppendLine("All recorded vaccinations are up to date.");
        sb.AppendLine();
        cites.Add(new AssistantCitation("pet", pet.Id.ToString(), pet.Name, $"/pets/{pet.Id}"));
    }

    // ---------- retrieval: knowledge ----------

    private static void AppendVaccination(StringBuilder sb, List<AssistantCitation> cites, AnimalType? species)
    {
        if (species is null) return;
        var plan = PetKnowledge.VaccinePlan(species.Value);
        if (plan.Count == 0) return;
        sb.AppendLine($"Typical vaccination schedule for {PetKnowledge.SpeciesKeyword(species.Value)}s (general guideline):");
        foreach (var item in plan)
            sb.AppendLine($"- {item.VaccineName}: from about {item.AgeWeeks} weeks" + (item.Notes is null ? "" : $" ({item.Notes})"));
        sb.AppendLine();
        cites.Add(new AssistantCitation("knowledge", $"vaccine-plan:{species}", $"{species} vaccination schedule", null));
    }

    private static void AppendCareKnowledge(StringBuilder sb, List<AssistantCitation> cites, AnimalType? species, PetFacts? pet, string intent)
    {
        if (species is null) return;
        var stage = PetKnowledge.LifeStage(species.Value, pet?.BirthDate);
        var tips = PetKnowledge.CareTips(species.Value, stage, pet?.Size);
        if (tips.Count > 0)
        {
            sb.AppendLine($"General care guidance for {PetKnowledge.SpeciesKeyword(species.Value)}s ({stage}):");
            foreach (var t in tips.Take(5)) sb.AppendLine($"- {t}");
            sb.AppendLine();
            cites.Add(new AssistantCitation("knowledge", $"care-tips:{species}:{stage}", $"{species} care tips", null));
        }
        if (intent == "nutrition" && PetKnowledge.HealthyRange(species.Value, pet?.Size) is { } range)
        {
            sb.AppendLine($"Healthy adult weight range: {range.MinKg:0.#}-{range.MaxKg:0.#} kg ({range.Basis}).");
            sb.AppendLine();
            cites.Add(new AssistantCitation("knowledge", $"weight-range:{species}", $"{species} healthy weight range", null));
        }
    }

    // ---------- retrieval: products & vets ----------

    private sealed record ProductHit(Guid Id, string Name, decimal Price, decimal? DiscountPrice);

    private async Task<List<ProductHit>> FindProductsAsync(string context, AnimalType? species, PetFacts? pet, CancellationToken ct)
    {
        var tokens = Tokens(context).ToList();
        if (species is { } s) tokens.Add(PetKnowledge.SpeciesKeyword(s));
        if (pet is not null && PetKnowledge.LifeStageKeyword(pet.AnimalType, PetKnowledge.LifeStage(pet.AnimalType, pet.BirthDate)) is { } ls)
            tokens.Add(ls);
        tokens = tokens.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();

        var scores = new Dictionary<Guid, (ProductHit Hit, double Score)>();
        foreach (var token in tokens)
        {
            var like = $"%{EscapeLike(token)}%";
            var rows = await _db.Products.AsNoTracking()
                .Where(p => p.IsActive && p.StockQuantity > 0 && p.Store.ApprovalStatus == ApprovalStatus.Approved
                            && (EF.Functions.ILike(p.Name, like) || (p.Description != null && EF.Functions.ILike(p.Description, like))))
                .OrderByDescending(p => p.RatingAverage).Take(15)
                .Select(p => new { p.Id, p.Name, p.Price, p.DiscountPrice, p.RatingAverage })
                .ToListAsync(ct);
            foreach (var r in rows)
            {
                var add = 1 + (double)r.RatingAverage / 10;
                scores[r.Id] = scores.TryGetValue(r.Id, out var cur)
                    ? (cur.Hit, cur.Score + add)
                    : (new ProductHit(r.Id, r.Name, r.Price, r.DiscountPrice), add);
            }
        }
        return scores.Values.OrderByDescending(v => v.Score).Take(3).Select(v => v.Hit).ToList();
    }

    private static void AppendProducts(StringBuilder sb, List<AssistantCitation> cites, List<ProductHit> products)
    {
        if (products.Count == 0) return;
        sb.AppendLine("Products from our store that match:");
        foreach (var p in products)
        {
            sb.AppendLine($"- {p.Name} - {(p.DiscountPrice ?? p.Price):0.##}");
            cites.Add(new AssistantCitation("product", p.Id.ToString(), p.Name, $"/store/products/{p.Id}"));
        }
        sb.AppendLine();
    }

    private sealed record VetHit(Guid Id, string Name, string? Specialty, string? City, string? Clinic);

    private async Task<List<VetHit>> FindVetsAsync(string context, AnimalType? species, string? city, CancellationToken ct)
    {
        IQueryable<Pawzaroo.Domain.Veterinary.Doctor> Base() =>
            _db.Doctors.AsNoTracking().Where(d => d.ApprovalStatus == ApprovalStatus.Approved);

        var q = Base();
        if (!string.IsNullOrWhiteSpace(city))
        {
            var like = $"%{EscapeLike(city)}%";
            var inCity = q.Where(d => d.City != null && EF.Functions.ILike(d.City, like));
            if (await inCity.AnyAsync(ct)) q = inCity;
        }

        var rows = await q.OrderByDescending(d => d.RatingAverage).Take(40)
            .Select(d => new
            {
                d.Id, Name = d.User.DisplayName, d.Specialty, d.City, d.ClinicName, d.RatingAverage,
                Types = d.SupportedAnimalTypes.Select(a => a.AnimalType).ToList()
            }).ToListAsync(ct);

        var tokens = Tokens(context).Where(t => t.Length >= 4).ToList();
        return rows
            .Select(r => new
            {
                r,
                Score = (r.Specialty is { } sp && tokens.Any(t => sp.Contains(t, StringComparison.OrdinalIgnoreCase)) ? 2 : 0)
                        + (species is { } s && r.Types.Contains(s) ? 1 : 0)
            })
            .OrderByDescending(x => x.Score).ThenByDescending(x => x.r.RatingAverage)
            .Take(3)
            .Select(x => new VetHit(x.r.Id, x.r.Name, x.r.Specialty, x.r.City, x.r.ClinicName))
            .ToList();
    }

    private static void AppendVets(StringBuilder sb, List<AssistantCitation> cites, List<VetHit> vets, string heading)
    {
        if (vets.Count == 0) return;
        sb.AppendLine($"{heading}:");
        foreach (var v in vets)
        {
            var detail = string.Join(", ", new[] { v.Specialty, v.Clinic, v.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
            sb.AppendLine($"- Dr. {v.Name}" + (detail.Length > 0 ? $" ({detail})" : ""));
            cites.Add(new AssistantCitation("vet", v.Id.ToString(), $"Dr. {v.Name}", $"/vets/{v.Id}"));
        }
        sb.AppendLine();
    }

    // ---------- emergency ----------

    private async Task<AssistantReply> BuildEmergencyAsync(TriageResult triage, PetFacts? pet, string? city, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"This sounds like a possible emergency ({triage.Category}). Contact a vet or an emergency animal clinic now - do not wait for an online answer.");
        sb.AppendLine("Call ahead so the clinic can prepare, keep your animal calm and warm, and do not give food, water or human medicine unless a vet tells you to.");
        sb.AppendLine($"Open the vet directory ({EscalationLink}) to find a vet near you.");
        sb.AppendLine();

        var cites = new List<AssistantCitation>();
        if (pet is not null)
        {
            sb.AppendLine($"Tell the vet: {pet.Name}, {pet.AnimalType}" + (pet.Breed is null ? "" : $", {pet.Breed}")
                + (pet.WeightKg is { } w ? $", {w:0.#} kg" : "") + (string.IsNullOrWhiteSpace(pet.Allergies) ? "" : $", allergies: {pet.Allergies}") + ".");
            sb.AppendLine();
            cites.Add(new AssistantCitation("pet", pet.Id.ToString(), pet.Name, $"/pets/{pet.Id}"));
        }
        var vets = await FindVetsAsync(triage.Matched ?? "", pet?.AnimalType, city, ct);
        AppendVets(sb, cites, vets, "Vets on Pawzaroo");
        cites.Add(new AssistantCitation("knowledge", $"triage:{triage.Category}", $"Emergency triage: {triage.Category}", EscalationLink));

        return new AssistantReply(sb.ToString().TrimEnd(), cites, true, "emergency", Disclaimer, EscalationLink);
    }

    // ---------- helpers ----------

    private static IEnumerable<string> Tokens(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[a-z][a-z\-]{2,}")
            .Select(m => m.Value).Where(t => !Stop.Contains(t)).Distinct();

    private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
