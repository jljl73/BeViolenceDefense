"""Prepare genuine Unity AI WAVs; originals and generation metadata are preserved separately."""
import array, json, math, shutil, sys, wave
from pathlib import Path
source, dest, report = map(Path, sys.argv[1:4])
dest.mkdir(parents=True, exist_ok=True)
records=[]
for path in sorted(source.glob('*.wav')):
    with wave.open(str(path)) as w:
        channels, width, rate, count = w.getnchannels(),w.getsampwidth(),w.getframerate(),w.getnframes()
        if width!=2: raise ValueError(f'Expected 16bit PCM: {path}')
        values=array.array('h',w.readframes(count))
    mono=[sum(values[i:i+channels])/channels for i in range(0,len(values),channels)]
    peak=max(map(abs,mono))
    if peak<32: raise ValueError(f'Silent/placeholder asset: {path}, peak {peak}')
    block=max(1,rate//100)
    levels=[math.sqrt(sum(v*v for v in mono[i:i+block])/len(mono[i:i+block])) for i in range(0,count,block)]
    threshold=max(2,max(levels)*.025)
    active=[i for i,rms in enumerate(levels) if rms>threshold]
    if not active: raise ValueError(f'No active sound: {path}')
    start=max(0,active[0]*block-int(.01*rate))
    end=min(count,(active[-1]+1)*block+int(.04*rate))
    gain=min((32767*10**(-9/20))/peak,10**(30/20))
    samples=mono[start:end]
    fadein=max(1,int(.002*rate));fadeout=max(1,int(.01*rate))
    cooked=array.array('h',(round(v*gain*min(1,i/fadein,(len(samples)-1-i)/fadeout)) for i,v in enumerate(samples)))
    target=dest/path.name
    with wave.open(str(target),'wb') as w:
        w.setparams((1,2,rate,len(cooked),'NONE','not compressed'));w.writeframes(cooked.tobytes())
    shutil.copy2(str(path)+'.meta',str(target)+'.meta')
    outpeak=max(map(abs,cooked))
    records.append(dict(file=path.name,source_seconds=count/rate,seconds=len(cooked)/rate,sample_rate=rate,channels=1,bits=16,trim_start_seconds=start/rate,gain_db=20*math.log10(gain),peak_dbfs=20*math.log10(outpeak/32767),clipped_samples=sum(abs(v)>=32767 for v in cooked)))
report.write_text(json.dumps(records,indent=2)+'\n')
print(f'Prepared {len(records)} clips; no silent assets or PCM clipping.')
