using Aurum.Models;

namespace Aurum.Services;

public static class ItemCategoryClassifier
{
    public static ItemMainCategory FromItemUiCategory(uint uiCategoryId)
    {
        if (uiCategoryId >= 1 && uiCategoryId <= 11) return ItemMainCategory.Combat;
        if (uiCategoryId >= 84 && uiCategoryId <= 89) return ItemMainCategory.Combat;
        if (uiCategoryId >= 12 && uiCategoryId <= 33) return ItemMainCategory.Crafting;
        if (uiCategoryId == 44 || uiCategoryId == 46 || uiCategoryId == 47) return ItemMainCategory.Consumable;
        if (uiCategoryId == 45 || (uiCategoryId >= 48 && uiCategoryId <= 56) || (uiCategoryId >= 58 && uiCategoryId <= 60))
            return ItemMainCategory.Material;
        if (IsHousingItemUiCategory(uiCategoryId)) return ItemMainCategory.Furniture;

        return ItemMainCategory.Unknown;
    }

    public static bool IsHousingItemUiCategory(uint uiCategoryId)
    {
        return uiCategoryId == 57
            || (uiCategoryId >= 64 && uiCategoryId <= 80)
            || uiCategoryId == 82;
    }
}
