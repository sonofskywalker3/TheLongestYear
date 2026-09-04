import json, math
objs={k:v.split('|') for k,v in json.load(open('objects.json')).items()}
name=lambda i:objs.get(i.replace('(O)',''),[i])[0]
crops=json.load(open('crops.json')); monsters=json.load(open('monsters.json'))
out=[]
# ---- crops: Jeff 2026-09-04: 100 tiles, no cost cap; only seed availability limits ----
SPECIAL={'(O)417':('16','Sweet Gem Berry: Rare Seed is cart-only, Spring and Summer, one per visit, 2 a week with an upgraded cart'),
         '(O)454':('5','Ancient Fruit: seed is a rare artifact, 28 days then weekly; about five a year from one plant'),
         '(O)433':('99','Coffee Bean: a 1% Dust Sprite drop in-loop, then 4 beans every 2 days and beans replant'),
         '(O)Carrot':('30','Carrot: Raccoon / tilling seeds, not buyable'),'(O)Broccoli':('30','Broccoli: Raccoon / tilling seeds'),
         '(O)SummerSquash':('30','Summer Squash: Raccoon / tilling seeds'),'(O)Powdermelon':('30','Powdermelon: Raccoon / tilling seeds')}
EXCLUDE={'(O)889','(O)832','(O)830','(O)771','(O)16','(O)396','(O)404','(O)412'}  # Qi Fruit, island crops, Fiber, wild-seed forage (forage table owns them)
out.append('    /// <summary>Crops, Jeff 2026-09-04: 100 tiles and no cost cap (seed makers, the stash and JP upgrades are\n    /// what multi-loop play is for), so only seed availability limits a crop. A shop seed is a full stack.</summary>\n    public static readonly IReadOnlyDictionary<string, double> Crops = new Dictionary<string, double>(StringComparer.Ordinal)\n    {')
seen=set()
for seed,c in crops.items():
    h=c['HarvestItemId']; h=h if h.startswith('(') else '(O)'+h
    if h in seen or h in EXCLUDE: continue
    seen.add(h)
    if h in SPECIAL: v,why=SPECIAL[h]
    else: v,why=('99',f'{name(h)}: shop seed')
    out.append(f'        ["{h}"] = {v},   // {why}')
out.append('    };\n')
# ---- monster drops: 60 kills a day x 7 days of the best reachable monster, every drop ----
SKIP={'Iridium Golem','Wilderness Golem','Fireball','Crow','Frog','Cat','Skeleton Mage','Pepper Rex','Tiger Slime','Lava Lurk','Hot Head','Magma Sprite','Magma Duggy','Magma Sparker','False Magma Cap','Dwarvish Sentry','Putrid Ghost','Shadow Sniper','Spider','Royal Serpent','Blue Squid','Truffle Crab'}
KILLS=60*7
best={}
for mon,v in monsters.items():
    if mon in SKIP: continue
    d=v.split('/')[6].split()
    per={}
    for i in range(0,len(d)-1,2):
        item=d[i]; ch=float(d[i+1])
        if item.startswith('-'): continue
        per[item]=per.get(item,0)+ch
    for item,ch in per.items():
        exp=KILLS*ch
        if exp>best.get(item,(0,''))[0]: best[item]=(exp,mon)
out.append('    /// <summary>Monster drops, Jeff 2026-09-04: 60 kills a day for 7 days of the best monster a loop can reach\n    /// (mines and Skull Cavern; volcano, island and the Qi dangerous-mines roster excluded), every drop in\n    /// Data/Monsters, chances summed per item. Capped at a stack.</summary>\n    public static readonly IReadOnlyDictionary<string, double> MonsterDrops = new Dictionary<string, double>(StringComparer.Ordinal)\n    {')
for item,(exp,mon) in sorted(best.items(), key=lambda kv:name('(O)'+kv[0])):
    if exp<2: continue
    out.append(f'        ["(O){item}"] = {min(99,exp):.1f},   // {name("(O)"+item)}: {mon}, {exp:.1f} expected')
out.append('    };\n')
# ---- minerals: base 4 ----
out.append('    /// <summary>Minerals (Data/Objects category -12), Jeff 2026-09-04: base 4; easier to hunt than a\n    /// specific artifact. Gems stay single.</summary>\n    public static readonly IReadOnlyDictionary<string, double> Minerals = new Dictionary<string, double>(StringComparer.Ordinal)\n    {')
for k,v in sorted(objs.items(), key=lambda kv:kv[1][0]):
    if v[1]=='cat=-12': out.append(f'        ["(O){k}"] = 4,   // {v[0]}')
out.append('    };')
open('tables.cs.txt','w').write('\n'.join(out)); print(len(out))
