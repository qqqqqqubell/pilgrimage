# 게임잼 SFX v2 (2026-11-15)

출처: Kenney (kenney.nl) Casino Audio · Impact Sounds · Interface Sounds · RPG Audio · Music Jingles — 모두 CC0(크레딧 불필요, 상업 이용 가능)
처리(v2): Kenney 원본에 합성 저음 붐(120→40Hz), 합성 성당 잔향(0.6~4초), STFT 합성 바람(좌→우), 종·합창 화음을 겹치고 tanh 포화로 밀도를 올림. 원본보다 6~15dB 크고 저음 비중이 큼. 피크 -1dB

## Unity 설정
1. `sfx/` 폴더를 `Assets/Resources/sfx/`에 복사 (짧은 소리는 Load Type = Decompress On Load)
2. 빈 오브젝트에 `SfxPlayer` 컴포넌트를 붙이고, BGM AudioSource가 있으면 `bgm` 칸에 연결
3. `SfxPlayer.Play("키")` 호출. `_01`, `_02` 변형은 같은 키에서 랜덤 재생됨

## 이벤트 → 키
| 이벤트 | 키 |
|---|---|
| 카드 hover / 버튼 | ui_hover / ui_click |
| 뒤집기(우클릭·스페이스) | card_flip |
| 드로우 (장마다) | card_draw |
| 카드 사용(더블클릭·엔터) | card_play + coin_spend |
| 덱에 카드 추가 ([신도], [포교] 등) | card_place |
| 덱 섞기 / 게임 시작 | deck_shuffle / deck_open |
| 4·8라운드 카드 획득 | card_reward |
| 헌금 획득 | coin_gain |
| 컷인 0.0초 | `SfxPlayer.Duck(0.9f)` |
| 컷인 0.1초 / 0.3초 | cutin_whoosh / cutin_impact (교회 종 타격음) |
| 컷인 0.6초 공격 적중 | hit_heavy + hit_slash (+ HitVfx.PlayHit) |
| [신도] → [이단] | heresy (+ HitVfx.PlayHeresy) |
| 신앙심·정합성 상승 / 하락 | buff (+buff_sparkle) / debuff |
| 라운드 시작 / 차례 변경 | round_start / turn_change |
| 결과 화면 | jingle_win / jingle_lose (후보 3개 중 하나만 남기기) |

## 생성 후보 (Kenney에 맞는 소리가 없음)
- cutin_whoosh: v2는 합성 바람+칼+저음으로 만듦
- 실제 합창·오르간 녹음 같은 질감은 합성으로 한계 → 필요하면 Stable Audio 생성
