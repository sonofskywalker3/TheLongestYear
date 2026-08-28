using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort for an animal product (Data/FarmAnimals + Data/Buildings): the housing chain
/// (Coop or Barn 1, Big 2, Deluxe 3, counted through BuildingToUpgrade) plus a price step from
/// the animal's purchase price, plus one for a deluxe produce (needs friendship) and one when the
/// animal takes more than a day per product. An animal Marnie does not sell (incubator hatch,
/// Ginger Island) takes a fixed step instead of a price step. Minimum over every animal that
/// makes the item.</summary>
public static class AnimalProductAvailability
{
    private const int BaseHousingEffort = 1;
    private const int CheapPrice = 1000;
    private const int MidPrice = 4000;
    private const int NotForSaleStep = 3;
    private const int DeluxeStep = 1;
    private const int SlowProduceStep = 1;
    private const int MaxChainDepth = 8;

    public static int PriceStep(int purchasePrice)
        => purchasePrice < CheapPrice ? 0 : purchasePrice < MidPrice ? 1 : 2;

    public static int HousingEffort(string building, IReadOnlyList<RawBuilding> buildings)
    {
        int links = 0;
        string? current = building;
        while (!string.IsNullOrEmpty(current) && links < MaxChainDepth)
        {
            RawBuilding? entry = buildings.FirstOrDefault(b => b.Name == current);
            if (entry == null || string.IsNullOrEmpty(entry.BuildingToUpgrade)) break;
            links++;
            current = entry.BuildingToUpgrade;
        }
        return BaseHousingEffort + links;
    }

    public static ItemEffort? Derive(
        string qualifiedId, IReadOnlyList<RawFarmAnimal> animals, IReadOnlyList<RawBuilding> buildings)
    {
        if (animals == null) throw new ArgumentNullException(nameof(animals));
        if (buildings == null) throw new ArgumentNullException(nameof(buildings));
        ItemEffort? best = null;
        foreach (RawFarmAnimal animal in animals)
        {
            bool regular = animal.ProduceIds.Contains(qualifiedId);
            bool deluxe = animal.DeluxeProduceIds.Contains(qualifiedId);
            if (!regular && !deluxe) continue;

            int housing = HousingEffort(animal.Building, buildings);
            bool forSale = animal.PurchasePrice >= 0;
            int price = forSale ? PriceStep(animal.PurchasePrice) : NotForSaleStep;
            int deluxeStep = regular ? 0 : DeluxeStep;
            int slow = animal.DaysToProduce > 1 ? SlowProduceStep : 0;
            int effort = housing + price + deluxeStep + slow;
            if (best == null || effort < best.Effort)
                best = new ItemEffort(effort,
                    $"animal product, {animal.Name} in {animal.Building} (housing {housing}), "
                    + (forSale ? $"price {animal.PurchasePrice}g (+{price})" : $"not for sale (+{price})")
                    + (deluxeStep > 0 ? ", deluxe produce (+1)" : "")
                    + (slow > 0 ? $", {animal.DaysToProduce} days per product (+1)" : "")
                    + $", effort {effort}");
        }
        return best;
    }
}
