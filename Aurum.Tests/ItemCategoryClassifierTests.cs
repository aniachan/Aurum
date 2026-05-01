using Aurum.Models;
using Aurum.Services;
using Xunit;

namespace Aurum.Tests;

public class ItemCategoryClassifierTests
{
    [Theory]
    [InlineData(57)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(66)]
    [InlineData(70)]
    [InlineData(73)]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(78)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(82)]
    public void FromItemUiCategory_ClassifiesHousingCategoriesAsFurniture(uint uiCategoryId)
    {
        Assert.Equal(ItemMainCategory.Furniture, ItemCategoryClassifier.FromItemUiCategory(uiCategoryId));
        Assert.True(ItemCategoryClassifier.IsHousingItemUiCategory(uiCategoryId));
    }

    [Theory]
    [InlineData(45)]
    [InlineData(48)]
    [InlineData(55)]
    [InlineData(58)]
    [InlineData(59)]
    [InlineData(60)]
    public void FromItemUiCategory_ClassifiesIngredientsAndCraftingInputsAsMaterials(uint uiCategoryId)
    {
        Assert.Equal(ItemMainCategory.Material, ItemCategoryClassifier.FromItemUiCategory(uiCategoryId));
        Assert.False(ItemCategoryClassifier.IsHousingItemUiCategory(uiCategoryId));
    }

    [Fact]
    public void FromItemUiCategory_DoesNotTreatMinionsAsHousing()
    {
        Assert.NotEqual(ItemMainCategory.Furniture, ItemCategoryClassifier.FromItemUiCategory(81));
        Assert.False(ItemCategoryClassifier.IsHousingItemUiCategory(81));
    }
}
