using Cartex.Application.Categories.Commands;
using Cartex.Application.Categories.Queries;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Products.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.Categories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class CategoryTreeAcceptanceTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record TestContext(long UserId, long BusinessId, long BranchId, long UnitId);

    [Fact]
    public async Task KAT_01_Create_and_move_reject_a_fourth_level_but_allow_the_third()
    {
        await AuthenticateAdminAsync();
        var electrical = await AddCategoryAsync(Unique("Elektr"), null, 0);
        var lighting = await AddCategoryAsync(Unique("Yoritish"), electrical, 0);
        var led = await AddCategoryAsync(Unique("LED"), lighting, 0);
        var movable = await AddCategoryAsync(Unique("Ko'chiriladigan"), null, 1);
        var tooDeepName = Unique("To'rtinchi daraja");

        var createError = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateCategoryAsync(tooDeepName, led));
        Assert.Equal("category_depth_exceeded", createError.Code);

        var moveError = await Assert.ThrowsAsync<BusinessRuleException>(
            () => MoveCategoryAsync(movable, led, 0));
        Assert.Equal("category_depth_exceeded", moveError.Code);

        var allowedThirdLevel = await CreateCategoryAsync(Unique("Uchinchi daraja"), lighting);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Categories.AnyAsync(x => x.Name == tooDeepName));
        Assert.Null(await db.Categories.Where(x => x.Id == movable).Select(x => x.ParentId).SingleAsync());
        Assert.Equal(lighting,
            await db.Categories.Where(x => x.Id == allowedThirdLevel).Select(x => x.ParentId).SingleAsync());
    }

    [Fact]
    public async Task KAT_02_A_category_cannot_be_moved_to_itself_or_any_descendant()
    {
        await AuthenticateAdminAsync();
        var electrical = await AddCategoryAsync(Unique("Elektr"), null, 0);
        var lighting = await AddCategoryAsync(Unique("Yoritish"), electrical, 0);
        var led = await AddCategoryAsync(Unique("LED"), lighting, 0);

        foreach (var forbiddenParent in new[] { electrical, lighting, led })
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(
                () => MoveCategoryAsync(electrical, forbiddenParent, 0));
            Assert.Equal("category_cycle", error.Code);
        }

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null(await db.Categories.Where(x => x.Id == electrical).Select(x => x.ParentId).SingleAsync());
        Assert.Equal(electrical,
            await db.Categories.Where(x => x.Id == lighting).Select(x => x.ParentId).SingleAsync());
        Assert.Equal(lighting,
            await db.Categories.Where(x => x.Id == led).Select(x => x.ParentId).SingleAsync());
    }

    [Fact]
    public async Task KAT_03_Reordering_renumbers_only_the_selected_sibling_group_and_keeps_product_links()
    {
        var context = await AuthenticateAdminAsync();
        var branchA = await AddCategoryAsync(Unique("A"), null, 0);
        var branchB = await AddCategoryAsync(Unique("B"), null, 1);
        var a1 = await AddCategoryAsync(Unique("A1"), branchA, 0);
        var a2 = await AddCategoryAsync(Unique("A2"), branchA, 1);
        var a3 = await AddCategoryAsync(Unique("A3"), branchA, 2);
        var b1 = await AddCategoryAsync(Unique("B1"), branchB, 0);
        var b2 = await AddCategoryAsync(Unique("B2"), branchB, 1);
        var productIds = new[]
        {
            await CreateProductAsync(Unique("A1 mahsulot"), a1, context.UnitId),
            await CreateProductAsync(Unique("A2 mahsulot"), a2, context.UnitId),
            await CreateProductAsync(Unique("A3 mahsulot"), a3, context.UnitId)
        };
        var productCategoriesBefore = await ProductCategoriesAsync(productIds);
        var countsBefore = await CategoryCountsAsync(branchA, branchB);

        await MoveCategoryAsync(a3, branchA, 0);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reorderedA = await db.Categories.AsNoTracking()
            .Where(x => x.ParentId == branchA)
            .OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.SortOrder })
            .ToListAsync();
        var untouchedB = await db.Categories.AsNoTracking()
            .Where(x => x.ParentId == branchB)
            .OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.SortOrder })
            .ToListAsync();

        Assert.Equal(new[] { a3, a1, a2 }, reorderedA.Select(x => x.Id));
        Assert.Equal(new[] { 0, 1, 2 }, reorderedA.Select(x => x.SortOrder));
        Assert.Equal(new[] { b1, b2 }, untouchedB.Select(x => x.Id));
        Assert.Equal(new[] { 0, 1 }, untouchedB.Select(x => x.SortOrder));

        var dtoSortOrders = await CategorySortOrdersAsync(a1, a2, a3, b1, b2);
        Assert.Equal(1, dtoSortOrders[a1]);
        Assert.Equal(2, dtoSortOrders[a2]);
        Assert.Equal(0, dtoSortOrders[a3]);
        Assert.Equal(0, dtoSortOrders[b1]);
        Assert.Equal(1, dtoSortOrders[b2]);

        var productCategoriesAfter = await ProductCategoriesAsync(productIds);
        Assert.Equal(productCategoriesBefore.Count, productCategoriesAfter.Count);
        Assert.All(productCategoriesBefore,
            pair => Assert.Equal(pair.Value, productCategoriesAfter[pair.Key]));
        var countsAfter = await CategoryCountsAsync(branchA, branchB);
        Assert.All(countsBefore, pair => Assert.Equal(pair.Value, countsAfter[pair.Key]));
    }

    [Fact]
    public async Task KAT_04_Reparenting_requires_categories_edit_and_a_successful_move_is_audited()
    {
        var context = await AuthenticateAdminAsync();
        var branchA = await AddCategoryAsync(Unique("A"), null, 0);
        var branchB = await AddCategoryAsync(Unique("B"), null, 1);
        var a2 = await AddCategoryAsync(Unique("A2"), branchA, 0);
        await AddCategoryAsync(Unique("B1"), branchB, 0);
        await CreateProductAsync(Unique("A2 mahsulot"), a2, context.UnitId);

        Fixture.CurrentUser.AsCashier(context.UserId, context.BusinessId, context.BranchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Categories.View);
        var countsBefore = await CategoryCountsAsync(branchA, branchB);
        Assert.Equal(1, countsBefore[branchA]);
        Assert.Equal(0, countsBefore[branchB]);

        await Assert.ThrowsAsync<ForbiddenException>(() => MoveCategoryAsync(a2, branchB, 0));

        using (var forbiddenScope = Fixture.CreateScope())
        {
            var db = forbiddenScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var unchanged = await db.Categories.AsNoTracking().SingleAsync(x => x.Id == a2);
            Assert.Equal(branchA, unchanged.ParentId);
            var countsAfterForbiddenMove = await CategoryCountsAsync(branchA, branchB);
            Assert.All(countsBefore,
                pair => Assert.Equal(pair.Value, countsAfterForbiddenMove[pair.Key]));
        }

        Fixture.CurrentUser.Granted.Add(AppPermissions.Categories.Edit);
        var firstAuditId = await MaxAuditIdAsync();
        await MoveCategoryAsync(a2, branchB, 0);

        var countsAfter = await CategoryCountsAsync(branchA, branchB);
        Assert.Equal(0, countsAfter[branchA]);
        Assert.Equal(1, countsAfter[branchB]);

        using var scope = Fixture.CreateScope();
        var verifyDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var moved = await verifyDb.Categories.AsNoTracking().SingleAsync(x => x.Id == a2);
        Assert.Equal(branchB, moved.ParentId);
        Assert.Equal(0, moved.SortOrder);

        var auditText = await CommandAuditTextAsync(firstAuditId, nameof(MoveCategoryCommand));
        AssertAuditField(auditText, "category", a2);
        AssertAuditField(auditText, "oldParent", branchA);
        AssertAuditField(auditText, "newParent", branchB);
        AssertAuditField(auditText, "sortOrder", 0);
    }

    [Fact]
    public async Task KAT_05_Full_path_is_computed_on_read_and_changes_immediately_after_an_ancestor_rename()
    {
        await AuthenticateAdminAsync();
        var oldRootName = Unique("Elektr mollari");
        var newRootName = Unique("Elektr jihozlari");
        var childName = Unique("Yoritgichlar");
        var root = await AddCategoryAsync(oldRootName, null, 0);
        var child = await AddCategoryAsync(childName, root, 0);

        var before = await CategoryDtoAsync(child);
        Assert.Equal(oldRootName + CategoryPath.Separator + childName, before.FullPath);

        await UpdateCategoryAsync(root, newRootName, null);

        var after = await CategoryDtoAsync(child);
        Assert.Equal(newRootName + CategoryPath.Separator + childName, after.FullPath);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var categoryType = db.Model.FindEntityType(typeof(Category));
        Assert.NotNull(categoryType);
        Assert.Null(categoryType.FindProperty(nameof(CategoryDto.FullPath)));
    }

    [Fact]
    public async Task KAT_06_Merge_moves_products_and_children_soft_deletes_source_and_audits_the_moved_count()
    {
        var context = await AuthenticateAdminAsync();
        var wrapper = await AddCategoryAsync(Unique("Katalog"), null, 0);
        var source = await AddCategoryAsync(Unique("A"), wrapper, 0);
        var target = await AddCategoryAsync(Unique("B"), wrapper, 1);
        var remainingSibling = await AddCategoryAsync(Unique("C"), wrapper, 2);
        var sourceChild = await AddCategoryAsync(Unique("A bola"), source, 0);
        var targetChild = await AddCategoryAsync(Unique("B bola"), target, 0);

        var sourceProducts = new List<long>();
        for (var index = 0; index < 7; index++)
            sourceProducts.Add(await CreateProductAsync(Unique($"A mahsulot {index}"), source, context.UnitId));

        var targetProducts = new List<long>();
        for (var index = 0; index < 3; index++)
            targetProducts.Add(await CreateProductAsync(Unique($"B mahsulot {index}"), target, context.UnitId));

        var firstAuditId = await MaxAuditIdAsync();
        await MergeCategoryAsync(source, target);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mergedProductIds = sourceProducts.Concat(targetProducts).ToArray();
        var productCategories = await db.Products.AsNoTracking()
            .Where(x => mergedProductIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CategoryId })
            .ToListAsync();
        Assert.Equal(10, productCategories.Count);
        Assert.All(productCategories, x => Assert.Equal(target, x.CategoryId));

        var deletedSource = await db.Categories.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.Id == source);
        Assert.True(deletedSource.IsDeleted);
        Assert.Equal(target,
            await db.Categories.Where(x => x.Id == sourceChild).Select(x => x.ParentId).SingleAsync());

        var wrapperSiblings = await db.Categories.AsNoTracking()
            .Where(x => x.ParentId == wrapper)
            .OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.SortOrder })
            .ToListAsync();
        Assert.Equal(new[] { target, remainingSibling }, wrapperSiblings.Select(x => x.Id));
        Assert.Equal(new[] { 0, 1 }, wrapperSiblings.Select(x => x.SortOrder));

        var targetChildren = await db.Categories.AsNoTracking()
            .Where(x => x.ParentId == target)
            .OrderBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.SortOrder })
            .ToListAsync();
        Assert.Equal(new[] { sourceChild, targetChild }.Order(), targetChildren.Select(x => x.Id).Order());
        Assert.Equal(new[] { 0, 1 }, targetChildren.Select(x => x.SortOrder));

        var targetDto = await CategoryDtoAsync(target);
        Assert.Equal(10, targetDto.DescendantProductCount);

        var auditText = await CommandAuditTextAsync(firstAuditId, nameof(MergeCategoryCommand));
        AssertAuditField(auditText, "source", source);
        AssertAuditField(auditText, "target", target);
        AssertAuditField(auditText, "moved", 7);
    }

    [Fact]
    public async Task KAT_06_Merge_rejects_the_same_category_and_a_descendant_without_changing_the_tree()
    {
        await AuthenticateAdminAsync();
        var source = await AddCategoryAsync(Unique("A"), null, 0);
        var child = await AddCategoryAsync(Unique("A bola"), source, 0);

        var sameCategoryError = await Assert.ThrowsAnyAsync<Exception>(
            () => MergeCategoryAsync(source, source));
        Assert.True(sameCategoryError is ValidationException or BusinessRuleException,
            sameCategoryError.GetType().Name);

        var descendantError = await Assert.ThrowsAnyAsync<Exception>(
            () => MergeCategoryAsync(source, child));
        Assert.True(descendantError is ValidationException or BusinessRuleException,
            descendantError.GetType().Name);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unchangedSource = await db.Categories.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.Id == source);
        Assert.False(unchangedSource.IsDeleted);
        Assert.Null(unchangedSource.ParentId);
        Assert.Equal(source,
            await db.Categories.Where(x => x.Id == child).Select(x => x.ParentId).SingleAsync());
    }

    [Fact]
    public async Task KAT_01_KAT_06_Merge_is_atomic_when_moving_source_children_would_create_a_fourth_level()
    {
        var context = await AuthenticateAdminAsync();
        var targetRoot = await AddCategoryAsync(Unique("Target root"), null, 0);
        var targetChild = await AddCategoryAsync(Unique("Target child"), targetRoot, 0);
        var targetGrandchild = await AddCategoryAsync(Unique("Target grandchild"), targetChild, 0);
        var source = await AddCategoryAsync(Unique("Source"), null, 1);
        var sourceChild = await AddCategoryAsync(Unique("Source child"), source, 0);
        var sourceProduct = await CreateProductAsync(Unique("Source product"), source, context.UnitId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => MergeCategoryAsync(source, targetGrandchild));
        Assert.Equal("category_depth_exceeded", error.Code);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unchangedSource = await db.Categories.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(x => x.Id == source);
        Assert.False(unchangedSource.IsDeleted);
        Assert.Null(unchangedSource.ParentId);
        Assert.Equal(source,
            await db.Categories.Where(x => x.Id == sourceChild).Select(x => x.ParentId).SingleAsync());
        Assert.Equal(source,
            await db.Products.Where(x => x.Id == sourceProduct).Select(x => x.CategoryId).SingleAsync());
    }

    private async Task<TestContext> AuthenticateAdminAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
        var businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
        var branchId = await db.Branches.Select(x => x.Id).FirstAsync();
        var unitId = await db.Units.Where(x => x.ShortName == "dona").Select(x => x.Id).FirstAsync();
        Fixture.CurrentUser.AsAdmin(userId, businessId, branchId);
        return new TestContext(userId, businessId, branchId, unitId);
    }

    /// Migratsiyadan keyin barcha `SortOrder` teng bo'ladi. Agar ko'chirish mantiqi
    /// o'qish so'rovidan boshqa tartibda ajratsa, birinchi sudrash butun ro'yxatni
    /// foydalanuvchi ko'rgan tartibdan boshqasiga o'tkazib yuboradi.
    [Fact]
    public async Task KAT_07_First_move_keeps_the_order_the_user_sees()
    {
        await AuthenticateAdminAsync();
        // Id tartibi (Zebra, Alfa, Beta) nom tartibidan (Alfa, Beta, Zebra) ataylab farq qiladi.
        var zebra = await AddCategoryAsync(Unique("ZZ Zebra"), null, 0);
        var alfa = await AddCategoryAsync(Unique("AA Alfa"), null, 0);
        var beta = await AddCategoryAsync(Unique("BB Beta"), null, 0);

        await MoveCategoryAsync(beta, null, 0);

        var orders = await CategorySortOrdersAsync(zebra, alfa, beta);
        Assert.True(orders[beta] < orders[alfa],
            $"sudralgan kategoriya birinchi bo'lishi kerak: beta={orders[beta]} alfa={orders[alfa]} zebra={orders[zebra]}");
        Assert.True(orders[alfa] < orders[zebra],
            $"qolganlari ko'rinib turgan tartibda qolishi kerak: beta={orders[beta]} alfa={orders[alfa]} zebra={orders[zebra]}");
    }

    private async Task<long> AddCategoryAsync(string name, long? parentId, int sortOrder)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Name = name, ParentId = parentId, SortOrder = sortOrder };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private async Task<long> CreateCategoryAsync(string name, long? parentId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateCategoryCommand(name, parentId));
    }

    private async Task UpdateCategoryAsync(long id, string name, long? parentId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new UpdateCategoryCommand(id, name, parentId));
    }

    private async Task MoveCategoryAsync(long id, long? parentId, int sortOrder)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new MoveCategoryCommand(id, parentId, sortOrder));
    }

    private async Task MergeCategoryAsync(long sourceId, long targetId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new MergeCategoryCommand(sourceId, targetId));
    }

    private async Task<long> CreateProductAsync(string name, long categoryId, long unitId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateProductCommand(name, categoryId, unitId, 0, null));
    }

    private async Task<Dictionary<long, long?>> ProductCategoriesAsync(IReadOnlyCollection<long> productIds)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.CategoryId);
    }

    private async Task<Dictionary<long, int>> CategoryCountsAsync(params long[] categoryIds)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var categories = await sender.Send(new GetCategoriesQuery { PageSize = 100 });
        return categories.Where(x => categoryIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.DescendantProductCount);
    }

    private async Task<Dictionary<long, int>> CategorySortOrdersAsync(params long[] categoryIds)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var categories = await sender.Send(new GetCategoriesQuery { PageSize = 100 });
        return categories.Where(x => categoryIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.SortOrder);
    }

    private async Task<CategoryDto> CategoryDtoAsync(long categoryId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var categories = await sender.Send(new GetCategoriesQuery { PageSize = 100 });
        return categories.Single(x => x.Id == categoryId);
    }

    private async Task<long> MaxAuditIdAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AuditLogs.MaxAsync(x => (long?)x.Id) ?? 0;
    }

    private async Task<string> CommandAuditTextAsync(long afterId, string commandName)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(x => x.Id > afterId && x.CommandName == commandName)
            .ToListAsync();
        Assert.NotEmpty(logs);
        return string.Join('|', logs.Select(x =>
            string.Join('|', x.Action, x.Summary, x.OldData, x.NewData, x.Details)));
    }

    private static void AssertAuditField(string auditText, string fieldName, long value)
    {
        Assert.Contains(fieldName, auditText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(value.ToString(), auditText, StringComparison.Ordinal);
    }

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";
}

public sealed class CategoryPathAcceptanceTests
{
    [Fact]
    public void KAT_05_Shared_path_helper_keeps_the_leaf_when_space_is_limited()
    {
        const string fullPath = "Elektr mollari / Yoritgichlar";
        const string expected = "… / Yoritgichlar";

        Assert.Equal(" / ", CategoryPath.Separator);
        Assert.Equal(expected, CategoryPath.Truncate(fullPath, expected.Length));
        Assert.NotEqual("Elektr mollari / …", CategoryPath.Truncate(fullPath, expected.Length));
    }
}
