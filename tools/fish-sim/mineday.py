# One "mine day": FLOORS floors fully cleared, each with TILES clear tiles and STONE_CHANCE of a stone per tile
# (populateLevel: stoneChance uniform 0.10..0.30, mean 0.20). Luck 0, Mining 5, no professions.
FLOORS=10; TILES=300; STONE=0.20; DAYS=7
stones=FLOORS*TILES*STONE          # 600 a day
tiles=FLOORS*TILES
out={}
# ore nodes: createLitterObject, 2.9% of stones on non-elevator floors; node drops num2 + Next(1,4) = 1..3 (mean 2), +luck/skill bonuses ~0.1
node=0.029*stones*2.1
# plain-stone ore: checkStoneForItems 5% x (1+num4) x 0.8 (1.2 for 40/42), then getOreIdForLevel by band
plain=stones*0.05*0.9
out['Copper Ore (floors 1-39)']=node+plain*0.95
out['Iron Ore (40-79)']=node+plain*0.75
out['Gold Ore (80-119)']=node+plain*0.75
out['Iridium Ore (Skull Cavern, judgement)']=10
# coal: 8% of 668/670 stones (10% of stones) x1, plus 25% of the 5% ore roll is coal instead (0.25 branch), plus Dust Sprites elsewhere
out['Coal']=stones*0.10*0.08 + stones*0.05*0.25
out['Stone']=stones*2
# geodes: 2.2% per stone (area geode), 0.5% omni above floor 20
out['Geode (any area)']=stones*0.022
out['Omni Geode']=stones*0.005
# floor items: itemChance 0.0025 per clear tile; 75% quartz, 25% the area crystal (0/10 Earth, 40 Frozen, 80 Fire)
items=tiles*0.0025
out['Quartz (floor item)']=items*0.75
out['Area crystal (Earth/Frozen/Fire, floor item)']=items*0.25
# geode contents add crystals: a geode is ~50% mineral, area crystal is 1 of ~9 minerals in the area geode
out['Area crystal (from geodes)']=out['Geode (any area)']*0.5/9
# gem nodes: gemStoneChance 0.003/2 per tile (+ level/24000), one gem each of 6 kinds by band
gemnodes=tiles*(0.0015+60/24000)
out['Gem nodes (all kinds)']=gemnodes
out['One specific gem (of 6)']=gemnodes/6
out['Diamond (0.00025 + level/120000 per stone, floor 60)']=stones*(0.00025+60/120000)
for k,v in out.items(): print(f"{k:50s} {v:6.1f}/day  {v*DAYS:6.1f}/week")
