import json,collections
best={tuple(k.split('|')):v for k,v in json.load(open('best.json')).items()}
names=json.load(open('objects.json'))
RAIN={'spring':0.18,'summer':0.15,'fall':0.18,'winter':0.0}
def day(f,s,rain,rate,h0,h1):
    return sum(rate*best.get((f,s,rain,str(t)),(0,))[0] for t in range(h0,h1,100))
fishes=sorted({k[0] for k in best if k[0].startswith('(O)')}, key=lambda f:names.get(f[3:],f))
print("| Fish | Season | Best spot | Sunny day, 20h | Rainy day, 20h | Weighted day, 20h | Weighted day, best 10h window |")
print("|---|---|---|---|---|---|---|")
for f in fishes:
  n=names.get(f[3:],f)
  for s in ['spring','summer','fall','winter']:
    sun=day(f,s,'False',2,600,2600); rn=day(f,s,'True',2,600,2600)
    if sun==0 and rn==0: continue
    w=(1-RAIN[s])*sun+RAIN[s]*rn; w10=max((1-RAIN[s])*day(f,s,'False',2,h,h+1000)+RAIN[s]*day(f,s,'True',2,h,h+1000) for h in range(600,1700,100))
    spots=collections.Counter(best[k][1]+('/'+best[k][2] if best[k][2] else '') for k in best if k[0]==f and k[1]==s)
    print(f"| {n} | {s} | {spots.most_common(1)[0][0]} | {sun:.1f} | {rn:.1f} | {w:.1f} | {w10:.1f} |")
