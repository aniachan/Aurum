using System;
using System.Collections.Generic;
using System.Linq;
using Aurum.Models;
using Aurum.Services;
using Dalamud.Plugin.Services;
using Moq;
using Xunit;

namespace Aurum.IntegrationTests.Services
{
    public class ItemPriorityServiceTests
    {
        private readonly ItemPriorityService _service;
        private readonly Mock<IPluginLog> _mockLog;
        private readonly Configuration _config;

        public ItemPriorityServiceTests()
        {
            _mockLog = new Mock<IPluginLog>();
            _config = new Configuration();
            _service = new ItemPriorityService(_mockLog.Object, _config);
        }

        private RecipeData CreateMockRecipe(int level, bool isExpert = false)
        {
            return new RecipeData
            {
                RecipeId = 1,
                ClassJobLevel = level,
                IsExpert = isExpert,
                ItemName = "Test Item"
            };
        }

        private MarketData CreateMockMarketData(float velocity, uint avgPrice)
        {
            return new MarketData
            {
                SaleVelocity = velocity,
                CurrentAveragePriceNQ = avgPrice,
                CurrentAveragePriceHQ = avgPrice
            };
        }

        [Fact]
        public void CalculatePriority_WithHighVelocityAndProfit_ReturnsHighScore()
        {
            // Arrange
            var recipe = CreateMockRecipe(90, true); // Max level, Expert
            var marketData = CreateMockMarketData(20, 150000); // Super high velocity, high price

            // Act
            var score = _service.CalculatePriority(recipe, marketData);

            // Assert
            // Recipe Level (90) -> 100 * 0.3 = 30
            // Category (Expert) -> 100 * 0.1 = 10
            // Velocity (20) -> 100 * 0.4 = 40
            // Profit (150k) -> 100 * 0.2 = 20
            // Total should be 100
            Assert.Equal(100, score);
        }

        [Fact]
        public void CalculatePriority_WithLowStats_ReturnsLowScore()
        {
            // Arrange
            var recipe = CreateMockRecipe(10); // Low level
            var marketData = CreateMockMarketData(0.05f, 100); // Dead item, cheap

            // Act
            var score = _service.CalculatePriority(recipe, marketData);

            // Assert
            // Recipe Level (<70) -> 10 * 0.3 = 3
            // Category (Normal) -> 50 * 0.1 = 5
            // Velocity (<0.1) -> 0 * 0.4 = 0
            // Profit (<1000) -> 10 * 0.2 = 2
            // Total = 10
            Assert.Equal(10, score);
        }

        [Fact]
        public void CalculatePriority_NoMarketData_BoostsCurrentContent()
        {
            // Arrange
            var recipe = CreateMockRecipe(90); // Current max level

            // Act
            var score = _service.CalculatePriority(recipe, null);

            // Assert
            // Recipe Level (90) -> 100 * 0.3 = 30
            // Category (Normal) -> 50 * 0.1 = 5
            // No Market Data Boost -> +20
            // Total = 55
            Assert.Equal(55, score);
        }

        [Fact]
        public void CalculatePriority_NoMarketData_LowLevel_NoBoost()
        {
            // Arrange
            var recipe = CreateMockRecipe(50);

            // Act
            var score = _service.CalculatePriority(recipe, null);

            // Assert
            // Recipe Level (50) -> 10 * 0.3 = 3
            // Category (Normal) -> 50 * 0.1 = 5
            // No Boost
            // Total = 8
            Assert.Equal(8, score);
        }

        [Theory]
        [InlineData(85, 35, true)]  // High priority, >30m ago -> Refresh
        [InlineData(85, 10, false)] // High priority, <30m ago -> Skip
        [InlineData(60, 130, true)] // Med priority, >2h ago -> Refresh
        [InlineData(60, 90, false)] // Med priority, <2h ago -> Skip
        [InlineData(10, 1500, true)] // Very low priority, >24h ago -> Refresh
        public void ShouldRefresh_RespectsThresholds(int score, int minutesSinceUpdate, bool expected)
        {
            // Arrange
            var lastUpdate = DateTime.UtcNow.AddMinutes(-minutesSinceUpdate);

            // Act
            var result = _service.ShouldRefresh(score, lastUpdate);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SortRecipesByPriority_OrdersCorrectly()
        {
            // Arrange
            var recipes = new[]
            {
                CreateMockRecipe(10), // Low score potential
                CreateMockRecipe(90)  // High score potential
            };

            // Mock market data provider to return consistent data
            // Give the level 90 recipe good stats, level 10 bad stats
            MarketData? GetMarketData(RecipeData r) => 
                r.ClassJobLevel == 90 ? CreateMockMarketData(10, 50000) : CreateMockMarketData(0, 100);

            // Act
            var sorted = _service.SortRecipesByPriority(recipes, GetMarketData);

            // Assert
            Assert.Equal(90, sorted[0].ClassJobLevel);
            Assert.Equal(10, sorted[1].ClassJobLevel);
        }
    }
}
