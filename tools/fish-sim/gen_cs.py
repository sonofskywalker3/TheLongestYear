import json,math
best={tuple(k.split('|')):v for k,v in json.load(open('best.json')).items()}
names=json.load(open('objects.json'))
RAIN={'spring':0.18,'summer':0.15,'fall':0.18,'winter':0.0}; RAINY_DAYS={'spring':5,'summer':4,'fall':5,'winter':0}
DAYS=7; RATE=2.0
LEGENDARY={'(O)163','(O)159','(O)160','(O)775','(O)682'}
def day(f,s,rain,h0,h1): return sum(RATE*best.get((f,s,rain,str(t)),(0,))[0] for t in range(h0,h1,100))
def best10(f,s,rain): return max(day(f,s,rain,h,h+1000) for h in range(600,1700,100))
fishes=sorted({k[0] for k in best if k[0].startswith('(O)') and k[0] not in LEGENDARY}, key=lambda f:names.get(f[3:],f))
lines=[]
for f in fishes:
  for s in ['spring','summer','fall','winter']:
    w=(1-RAIN[s])*best10(f,s,'False')+RAIN[s]*best10(f,s,'True')
    rainy=best10(f,s,'True')*RAINY_DAYS[s]
    basis=max(w*DAYS, rainy)
    if w==0 and rainy==0: continue
    if basis<2: continue   # a fish you land once a week or less is a single ask
    lines.append(f'            [(Season.{s.capitalize()}, "{f}")] = {basis:.1f},   // {names.get(f[3:],f)}: {w:.1f}/day x {DAYS}' + (f', rainy {rainy:.1f}' if rainy>w*DAYS else ''))
open('basis.cs.txt','w').write('\n'.join(lines)); print(len(lines)); print('\n'.join(lines[:6]))
