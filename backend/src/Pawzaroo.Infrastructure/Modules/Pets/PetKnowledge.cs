using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>
/// Static species knowledge used for schedules, ranges and tips. These are
/// general guidelines — the UI always tells owners to confirm with their vet.
/// Move to a DB-backed table if admins need to edit them.
/// </summary>
internal static class PetKnowledge
{
    private static readonly Dictionary<AnimalType, VaccinePlanItem[]> VaccinePlans = new()
    {
        [AnimalType.Dog] = new[]
        {
            new VaccinePlanItem("DHPP (1st dose)", 6, "Distemper, hepatitis, parvovirus, parainfluenza"),
            new VaccinePlanItem("DHPP (2nd dose)", 10, null),
            new VaccinePlanItem("Leptospirosis", 12, null),
            new VaccinePlanItem("Rabies", 14, "Required by law in most regions"),
            new VaccinePlanItem("DHPP (3rd dose)", 14, null),
            new VaccinePlanItem("DHPP booster", 52, "Then every 1–3 years"),
            new VaccinePlanItem("Rabies booster", 66, "Then yearly or as required locally"),
        },
        [AnimalType.Cat] = new[]
        {
            new VaccinePlanItem("FVRCP (1st dose)", 8, "Rhinotracheitis, calicivirus, panleukopenia"),
            new VaccinePlanItem("FeLV (1st dose)", 8, "Feline leukaemia — for outdoor cats"),
            new VaccinePlanItem("FVRCP (2nd dose)", 12, null),
            new VaccinePlanItem("FeLV (2nd dose)", 12, null),
            new VaccinePlanItem("FVRCP (3rd dose)", 16, null),
            new VaccinePlanItem("Rabies", 16, null),
            new VaccinePlanItem("FVRCP booster", 68, "Then every 1–3 years"),
        },
        [AnimalType.Rabbit] = new[]
        {
            new VaccinePlanItem("Myxomatosis", 6, null),
            new VaccinePlanItem("RHDV1 / RHDV2", 10, "Rabbit haemorrhagic disease"),
            new VaccinePlanItem("RHDV booster", 62, "Yearly"),
        },
        [AnimalType.Cow] = new[]
        {
            new VaccinePlanItem("Foot-and-mouth disease (FMD)", 16, "Booster every 6 months"),
            new VaccinePlanItem("Haemorrhagic septicaemia (HS)", 26, "Before monsoon"),
            new VaccinePlanItem("Black quarter (BQ)", 26, null),
            new VaccinePlanItem("Anthrax", 26, "In endemic areas"),
            new VaccinePlanItem("Lumpy skin disease", 16, null),
        },
        [AnimalType.Goat] = new[]
        {
            new VaccinePlanItem("PPR", 12, "Peste des petits ruminants"),
            new VaccinePlanItem("Enterotoxaemia", 12, null),
            new VaccinePlanItem("Foot-and-mouth disease (FMD)", 16, null),
            new VaccinePlanItem("Goat pox", 16, null),
        },
        [AnimalType.Sheep] = new[]
        {
            new VaccinePlanItem("PPR", 12, null),
            new VaccinePlanItem("Enterotoxaemia", 12, null),
            new VaccinePlanItem("Foot-and-mouth disease (FMD)", 16, null),
            new VaccinePlanItem("Sheep pox", 16, null),
        },
        [AnimalType.Horse] = new[]
        {
            new VaccinePlanItem("Tetanus", 16, null),
            new VaccinePlanItem("Eastern/Western encephalomyelitis", 16, null),
            new VaccinePlanItem("Rabies", 16, null),
            new VaccinePlanItem("Equine influenza", 24, null),
        },
        [AnimalType.Chicken] = new[]
        {
            new VaccinePlanItem("Marek's disease", 0, "Day-old"),
            new VaccinePlanItem("Newcastle disease (ND)", 1, null),
            new VaccinePlanItem("Infectious bursal disease (Gumboro)", 2, null),
            new VaccinePlanItem("Fowl pox", 6, null),
        },
        [AnimalType.Duck] = new[]
        {
            new VaccinePlanItem("Duck plague", 8, null),
            new VaccinePlanItem("Duck cholera", 10, null),
        },
    };

    public static IReadOnlyList<VaccinePlanItem> VaccinePlan(AnimalType t) =>
        VaccinePlans.TryGetValue(t, out var plan) ? plan : Array.Empty<VaccinePlanItem>();

    /// <summary>Typical gestation / incubation length in days.</summary>
    public static int? GestationDays(AnimalType t) => t switch
    {
        AnimalType.Dog => 63,
        AnimalType.Cat => 65,
        AnimalType.Rabbit => 31,
        AnimalType.Cow => 283,
        AnimalType.Goat => 150,
        AnimalType.Sheep => 147,
        AnimalType.Horse => 340,
        AnimalType.Chicken => 21,
        AnimalType.Duck => 28,
        _ => null
    };

    public static WeightRangeDto? HealthyRange(AnimalType t, AnimalSize? size) => t switch
    {
        AnimalType.Dog => size switch
        {
            AnimalSize.Tiny => new WeightRangeDto(1, 4, "Adult dog · tiny breed"),
            AnimalSize.Small => new WeightRangeDto(4, 10, "Adult dog · small breed"),
            AnimalSize.Large => new WeightRangeDto(25, 45, "Adult dog · large breed"),
            AnimalSize.ExtraLarge => new WeightRangeDto(45, 90, "Adult dog · giant breed"),
            _ => new WeightRangeDto(10, 25, "Adult dog · medium breed"),
        },
        AnimalType.Cat => new WeightRangeDto(3, 6, "Adult domestic cat"),
        AnimalType.Rabbit => new WeightRangeDto(1, 6, "Adult rabbit"),
        AnimalType.Goat => new WeightRangeDto(20, 80, "Adult goat"),
        AnimalType.Sheep => new WeightRangeDto(35, 100, "Adult sheep"),
        AnimalType.Cow => new WeightRangeDto(300, 800, "Adult cow"),
        AnimalType.Horse => new WeightRangeDto(380, 600, "Adult horse"),
        AnimalType.Chicken => new WeightRangeDto(1.5m, 4, "Adult chicken"),
        AnimalType.Duck => new WeightRangeDto(1.5m, 4, "Adult duck"),
        _ => null
    };

    public static string LifeStage(AnimalType t, DateTime? birthDate)
    {
        if (birthDate is null) return "Adult";
        var years = (DateTime.UtcNow - birthDate.Value).TotalDays / 365.25;
        var seniorAt = t switch { AnimalType.Dog => 7, AnimalType.Cat => 10, AnimalType.Rabbit => 5, AnimalType.Horse => 18, _ => 8 };
        if (years < 1) return t switch { AnimalType.Dog => "Puppy", AnimalType.Cat => "Kitten", _ => "Young" };
        return years >= seniorAt ? "Senior" : "Adult";
    }

    public static IReadOnlyList<string> CareTips(AnimalType t, string lifeStage, AnimalSize? size)
    {
        var tips = new List<string>();
        switch (lifeStage)
        {
            case "Puppy" or "Kitten" or "Young":
                tips.Add("Feed a growth formula in 3–4 small meals a day until about 6 months old.");
                tips.Add("Keep up the vaccination series — immunity isn't complete until the last dose.");
                tips.Add("Start socialisation and gentle handling early.");
                break;
            case "Senior":
                tips.Add("Book a vet check-up every 6 months, including bloodwork.");
                tips.Add("Switch to a senior diet that supports joints and kidneys.");
                tips.Add("Watch for weight changes, stiffness or changes in thirst.");
                break;
            default:
                tips.Add("Schedule a yearly wellness exam and keep vaccinations current.");
                tips.Add("Weigh monthly to catch weight gain early.");
                break;
        }

        switch (t)
        {
            case AnimalType.Dog:
                tips.Add(size is AnimalSize.Large or AnimalSize.ExtraLarge
                    ? "Large breeds are prone to joint issues — avoid over-exercise while growing."
                    : "Daily walks of 30–60 minutes keep most dogs fit and calm.");
                tips.Add("Deworm every 3 months and use tick/flea prevention.");
                break;
            case AnimalType.Cat:
                tips.Add("Provide scratching posts and keep the litter box clean daily.");
                tips.Add("Encourage water intake — a fountain helps prevent urinary issues.");
                break;
            case AnimalType.Rabbit:
                tips.Add("Unlimited hay should make up ~80% of the diet.");
                break;
            case AnimalType.Bird:
                tips.Add("Avoid non-stick cookware fumes — they are toxic to birds.");
                break;
            case AnimalType.Cow or AnimalType.Goat or AnimalType.Sheep:
                tips.Add("Deworm before and after the monsoon; rotate pastures.");
                tips.Add("Provide mineral licks and clean drinking water at all times.");
                break;
            case AnimalType.Chicken or AnimalType.Duck:
                tips.Add("Keep litter dry and clean to prevent coccidiosis.");
                break;
        }
        return tips;
    }

    public static string SpeciesKeyword(AnimalType t) => t switch
    {
        AnimalType.Dog => "dog",
        AnimalType.Cat => "cat",
        AnimalType.Bird => "bird",
        AnimalType.Rabbit => "rabbit",
        AnimalType.Fish => "fish",
        AnimalType.Reptile => "reptile",
        AnimalType.Cow => "cattle",
        AnimalType.Goat => "goat",
        AnimalType.Sheep => "sheep",
        AnimalType.Horse => "horse",
        AnimalType.Chicken => "poultry",
        AnimalType.Duck => "duck",
        _ => "pet"
    };

    public static string? LifeStageKeyword(AnimalType t, string lifeStage) => lifeStage switch
    {
        "Puppy" => "puppy",
        "Kitten" => "kitten",
        "Senior" => "senior",
        _ => null
    };
}
