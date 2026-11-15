#!/usr/bin/env python3
"""Import supplied card images, retaining original archive/file correspondence."""
import argparse, concurrent.futures, json, pathlib, shutil, subprocess, tempfile, unicodedata, zipfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
BACKS=[45,33,37,40,36,39,35,38,34,25,29,32,28,31,27,30,26,41,43,47,42,44]
GENERAL=['Follower','RequestOffering','ClergyTraining','Evangelism','Edict','Preaching','BondOfFaith','AggressiveMission','Penance','SolitaryReflection','CaravanInvestment','Council','WitchHunt','Reaper','RegularWorship','Heretic','PublicDenunciation','GlimmerOfHope','FalseRevelation','Blessing','ReturnToFaith','Paradise']
FACTION={'빈민 구휼':'PoorRelief','구세군 모금':'SalvationFundraising','자선냄비':'CharityPot','누추한 성전':'HumbleTemple','십일조':'Tithe','이자':'Interest','면죄부':'Indulgence','성전 리모델링':'TempleRemodeling','교단 간 고리대금업':'InterfaithLoan','대규모 선동':'MassAgitation','대주교':'Archbishop','이단 심문관':'Inquisitor','이단 심판':'HeresyJudgment','성서무오론':'Inerrancy','고해성사':'Confession','지역 정화':'AreaCleansing','신실한 신도':'FaithfulFollower','거부하는 신도':'RefusingFollower','털갈이 의식':'MoltingRitual','깊은 숲의 성역':'DeepForestSanctuary','야성의 포효':'WildRoar','털가죽의 부름':'CallOfFur'}
def main():
 p=argparse.ArgumentParser();p.add_argument('general_zip');p.add_argument('faction_zip');a=p.parse_args()
 target=ROOT/'Assets/Pilgrimage/Cards/Art';target.mkdir(parents=True,exist_ok=True)
 rows=[];jobs=[]
 for number,card_id in enumerate(GENERAL):
  front=f'General_{number:02}_Front.png';back=f'General_{number:02}_Back.png'
  rows.append(dict(id=card_id,number=number,front=front,back=back,sourceFront=f'{number}.png',sourceBack=f'{BACKS[number]}.png'))
  jobs.extend([(a.general_zip,f'{number}.png',front),(a.general_zip,f'{BACKS[number]}.png',back)])
 jobs.append((a.general_zip,'46.png','Unknown_Back.png'))
 with zipfile.ZipFile(a.faction_zip) as z:
  for name in z.namelist():
   normalized=unicodedata.normalize('NFC',name);stem=pathlib.PurePosixPath(normalized).stem
   if normalized.startswith('__MACOSX') or not normalized.endswith('.png'):continue
   if stem not in FACTION:raise ValueError('Unmapped faction art: '+name)
   card_id=FACTION[stem];front='Faction_'+card_id+'.png';jobs.append((a.faction_zip,name,front))
   rows.append(dict(id=card_id,number=-1,front=front,back='Unknown_Back.png',sourceFront=normalized,sourceBack='46.png'))
 fur_art = (target/'Faction_FurRally.png').exists()
 rows.append(dict(id='FurRally',number=-1,front='Faction_FurRally.png' if fur_art else '',back='Unknown_Back.png',sourceFront='1222.png' if fur_art else '',sourceBack='46.png',missingArt=not fur_art))
 assert len(rows)==45 and len({row['id'] for row in rows})==45
 def prepare(job):
  archive,source,destination=job
  with tempfile.TemporaryDirectory(prefix='pilgrimage-card-') as tmp:
   raw=pathlib.Path(tmp)/'source.png'
   with zipfile.ZipFile(archive) as z:raw.write_bytes(z.read(source))
   if shutil.which('sips'):
    subprocess.run(['sips','-Z','2048',str(raw),'--out',str(target/destination)],check=True,stdout=subprocess.DEVNULL)
   else:shutil.copy2(raw,target/destination)
  return destination
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
  for i,name in enumerate(pool.map(prepare,jobs),1):print(f'{i}/{len(jobs)} {name}',flush=True)
 (target.parent/'CardArtSources.json').write_text(json.dumps(dict(cards=rows),ensure_ascii=False,indent=2)+'\n')
 print(f'Imported {len(jobs)} images; 45 card mappings.')
if __name__=='__main__':main()
