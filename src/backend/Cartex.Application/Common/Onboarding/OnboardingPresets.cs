using Cartex.Domain.Authorization;

namespace Cartex.Application.Common.Onboarding;

public sealed record PresetProductType(IReadOnlyDictionary<string, string> Names, bool TracksExpiry)
{
    public string Name(string? language) => Names.GetValueOrDefault(language ?? "", Names["uz-latn"]);
}

public sealed record OnboardingPreset(string Code, string[] EnableFeatures, PresetProductType[] ProductTypes);

public static class OnboardingPresets
{
    private static PresetProductType Type(string uzLatn, string uzCyrl, string ru, string en, bool expiry) =>
        new(new Dictionary<string, string> { ["uz-latn"] = uzLatn, ["uz-cyrl"] = uzCyrl, ["ru"] = ru, ["en"] = en }, expiry);

    public static readonly IReadOnlyList<OnboardingPreset> All =
    [
        new("minimarket", [],
            [Type("Tez buziladigan", "Тез бузиладиган", "Скоропортящийся", "Perishable", true),
             Type("Tortiladigan", "Тортиладиган", "Весовой", "Weighed", false)]),
        new("construction", [FeatureCatalog.Multicurrency],
            [Type("O'lchanadigan material", "Ўлчанадиган материал", "Мерный материал", "Measured material", false)]),
        new("clothing", [],
            [Type("Kiyim", "Кийим", "Одежда", "Clothing", false),
             Type("Poyabzal", "Пойабзал", "Обувь", "Footwear", false)]),
        new("electronics", [FeatureCatalog.Multicurrency], []),
        new("pharmacy", [],
            [Type("Dori vositasi", "Дори воситаси", "Лекарственное средство", "Medicine", true)]),
        new("universal", [], []),
    ];

    public static OnboardingPreset? Find(string? code) => All.FirstOrDefault(p => p.Code == code);
}
