import json,collections,sys
best={tuple(k.split('|')):v for k,v in json.load(open('best.json')).items()}
names=json.load(open('objects.json'))
RATE=float(sys.argv[1]); H0=int(sys.argv[2]); H1=int(sys.argv[3])   # catches per game hour, window start/end (600..2600)
RAIN={'spring':0.18,'summer':0.15,'fall':0.18,'winter':0.0}
out=collections.defaultdict(dict)
fishes=sorted({k[0] for k in best})
for f in fishes:
  for s in ['spring','summer','fall','winter']:
    day={}
    for rain in ('False','True'):
        tot=0; hrs=0
        for tod in range(H0,H1,100):
            p=best.get((f,s,rain,str(tod)),(0,))[0]
            tot+=RATE*p; hrs+= p>0
        day[rain]=(tot,hrs)
    exp=(1-RAIN[s])*day['False'][0]+RAIN[s]*day['True'][0]
    if exp>0: out[f][s]=(round(exp,2),day['False'][1],day['True'][1])
print(f"catch rate {RATE}/hr, window {H0}-{H1}  (expected fish/day, hours available sunny, hours available rainy)")
for f in fishes:
    n=names.get(f.replace('(O)',''),f)
    print(f"{n:22s}", '  '.join(f"{s[:2]} {v[0]:5.1f} ({v[1]:2d}h/{v[2]:2d}h)" for s,v in out[f].items()))
