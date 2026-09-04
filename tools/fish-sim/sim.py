import json, random, re, collections, sys
rows=json.load(open('locations_fish.json')); fishdata=json.load(open('fish.json')); names=json.load(open('objects.json'))
LEVEL=10; DEPTH=5; DAILY_LUCK=0.0
SEASONS=['spring','summer','fall','winter']
# places a normal loop can fish; (location, fishArea or None)
LOCS=['Town','Beach','Forest','Mountain','Sewer','UndergroundMine','Farm_Standard','Desert','Woods','Railroad','Backwoods','BusStop']
PLACES=[]
for _l in LOCS:
    _areas={r['FishAreaId'] for r in rows.get(_l,[])}
    for _a in (_areas or {None}): PLACES.append((_l,_a))
def cond_ok(c,season,rain):
    if not c: return True
    for part in [p.strip() for p in c.split(',')]:
        neg=part.startswith('!'); q=part[1:] if neg else part; t=q.split()
        k=t[0]
        if k=='LOCATION_SEASON': ok=season in t[2:]
        elif k=='IS_FESTIVAL_DAY': ok=False
        elif k=='SEASON': ok=season in t[1:]
        elif k=='WEATHER': ok=rain
        elif k in('PLAYER_SPECIAL_ORDER_RULE_ACTIVE','IS_PASSIVE_FESTIVAL_OPEN','IS_ISLAND_NORTH_BRIDGE_FIXED','PLAYER_HAS_ITEM','YEAR','TIME'): ok=False
        elif k=='PLAYER_HAS_MAIL': ok=False
        elif k=='RANDOM': ok=random.random()<float(t[1])
        else: ok=False
        if neg: ok=not ok
        if not ok: return False
    return True
def generic_ok(itemid,season,rain,tod):
    fid=itemid.replace('(O)','')
    d=fishdata.get(fid)
    if d is None: return True
    a=d.split('/')
    if a[1]=='trap': return True
    spans=a[5].split(); ok=False
    for i in range(0,len(spans),2):
        if int(spans[i])<=tod<int(spans[i+1]): ok=True
    if not ok: return False
    w=a[7]
    if w=='rainy' and not rain: return False
    if w=='sunny' and rain: return False
    if LEVEL<int(a[12]): return False
    maxd=int(a[9]); ch=float(a[10]); mult=float(a[11])
    ch-= max(0,maxd-DEPTH)*mult*ch
    ch+= LEVEL/50
    ch=min(ch,0.9)
    return random.random()<ch
def cast(loc,area,season,rain,tod):
    pool=rows['Default']+rows.get(loc,[])
    pool=sorted(pool,key=lambda r:(r['Precedence'],random.random()))
    for r in pool:
        if r['FishAreaId'] and r['FishAreaId']!=area: continue
        if r['Season'] and r['Season'].lower()!=season: continue
        if LEVEL<r['MinFishingLevel']: continue
        if DEPTH<r['MinDistanceFromShore']: continue
        if r['MaxDistanceFromShore']>-1 and DEPTH>r['MaxDistanceFromShore']: continue
        if r['RequireMagicBait'] or r['BobberPosition'] or r['PlayerPosition']: continue
        if random.random()>=r['Chance']: continue
        if not cond_ok(r['Condition'],season,rain): continue
        item=r['ItemId'] or 'TRASH'
        if item.startswith('SECRET'): item='TRASH'
        if '|' in item: item='TRASH'
        if item!='TRASH' and not generic_ok(item,season,rain,tod): continue
        return item
    return 'TRASH'
N=int(sys.argv[1]) if len(sys.argv)>1 else 600
random.seed(1)
# p[(fish,season,rain,tod)] = best over places of per-cast prob; also keep the place
best={}
for season in SEASONS:
  for rain in (False,True):
    for tod in range(600,2600,100):
      for loc,area in PLACES:
        c=collections.Counter(cast(loc,area,season,rain,tod) for _ in range(N))
        for item,n in c.items():
            if item=='TRASH': continue
            p=n/N; key=(item,season,rain,tod)
            if p>best.get(key,(0,))[0]: best[key]=(p,loc,area)
json.dump({'|'.join(map(str,k)):v for k,v in best.items()},open('best.json','w'))
print('done',len(best))
