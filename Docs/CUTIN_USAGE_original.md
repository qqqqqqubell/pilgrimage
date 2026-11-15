# 컷인 + 슬래시 트랜지션 사용법

공격 카드를 쓸 때 나오는 페르소나식 컷인 연출용 스프라이트 묶음입니다.
캐릭터 포트레이트는 들어 있지 않으며, 교단별 포트레이트를 따로 끼워 넣어 사용합니다.

---

## 1. 파일 구성

| 파일 | 내용 |
|---|---|
| `cutin_bg_sheet.png` | 포트레이트 **뒤**에 깔리는 빨간 사선 띠 (흐르는 줄무늬 포함) |
| `cutin_mask_sheet.png` | 포트레이트가 **보이는 영역** (흰색 = 보임). SpriteMask용 |
| `cutin_fg_sheet.png` | 포트레이트 **앞**에 덮이는 찢긴 흰 종이 테두리, 검은 외곽선, 흰 번쩍임, 충격선, 파편 |
| `cutin_meta.json` | FPS, 프레임 수, 프레임별 암전 알파값, 포트레이트 위치/크기, 이벤트 프레임 |
| `CutInPlayer.cs` | Unity 재생 스크립트 |
| `cutin_preview.gif` | 합성 미리보기 (살구색 타원 = 포트레이트 자리) |
| `make_cutin.py` | 시트를 다시 생성하는 Python 스크립트 |

### 시트 규격 (세 장 공통)

- 프레임 크기: **960 × 540**
- 배치: **가로 4 × 세로 6 = 24프레임**, 왼쪽→오른쪽, 위→아래 순
- 시트 크기: 3840 × 3240
- 재생 속도: **30fps**, 총 **0.8초**

---

## 2. 타임라인 (Notion 모션 플롯 기준)

| 프레임 | 시간 | 연출 | 이벤트 |
|---|---|---|---|
| 0–2 | 0.0–0.1초 | 화면 암전, 전투 정지 | `battle_pause` (0) |
| 3–8 | 0.1–0.3초 | 사선 컷인이 왼쪽에서 화면을 가로지르며 등장, 포트레이트가 띠를 따라 미끄러져 들어옴 | `cutin_enter` (3) |
| 9–11 | 0.3초 | 흰색 번쩍임 + 충격선 + 별 모양 임팩트, 흔들림 (9–12) | `flash_impact` (9) → 타격음 |
| 12–17 | 0.4–0.6초 | 유지 | |
| 18–21 | 0.6–0.7초 | 컷인이 오른쪽으로 빠르게 퇴장, 암전 해제 시작 | `cutin_exit_skill_fire` (18) → 카드 스킬 발동 |
| 22–23 | 0.7–0.8초 | 원래 화면 복귀 | `return_to_game` (24) |

---

## 3. 레이어 순서 (뒤 → 앞)

```
0  Dim       검은색 전체 화면 (알파는 meta.json의 dim_alpha)
1  BandBG    cutin_bg_sheet
   Mask      cutin_mask_sheet  ← Portrait만 이 영역 안에서 보임
2  Portrait  교단 캐릭터 포트레이트
3  BandFG    cutin_fg_sheet
```

번쩍임은 앞 레이어(FG)에 있어서 포트레이트까지 순간적으로 하얗게 덮습니다.

---

## 4. Unity 적용

### 4-1. 시트 임포트

세 장 모두 아래처럼 설정합니다.

1. Texture Type: **Sprite (2D and UI)**, Sprite Mode: **Multiple**
2. Max Size: **4096** (기본 2048이면 축소되어 흐려짐)
3. Compression: None 또는 High Quality 권장 (테두리 뭉개짐 방지)
4. Sprite Editor → Slice → Type **Grid By Cell Size**, Pixel Size **960 × 540**, Pivot **Center** → Slice → Apply
5. Pixels Per Unit은 세 장 모두 같게 맞춥니다 (스크립트의 `pixelsPerUnit`과도 동일하게).

### 4-2. 오브젝트 구성

카메라 중앙에 빈 오브젝트 `CutIn`을 만들고 자식을 다음처럼 둡니다.

| 자식 | 컴포넌트 | 설정 |
|---|---|---|
| `Dim` | SpriteRenderer | 흰색 1×1 스프라이트, Color 검정, 화면을 덮도록 Scale 조정, Order 0 |
| `BandBG` | SpriteRenderer | Order 1 |
| `Mask` | SpriteMask | Alpha Cutoff 0.5 정도 |
| `Portrait` | SpriteRenderer | **Mask Interaction = Visible Inside Mask**, Order 2 |
| `BandFG` | SpriteRenderer | Order 3 |

960×540 프레임이므로 1920×1080 화면을 채우려면 `CutIn` 오브젝트의 Scale을 2배로 하거나 PPU를 절반으로 잡습니다.

### 4-3. 스크립트 연결

1. `CutIn`에 `CutInPlayer.cs`를 붙입니다.
2. `Bg Frames`, `Fg Frames`, `Mask Frames`에 슬라이스된 스프라이트 24장씩을 **번호 순서대로** 넣습니다. (Project 창에서 시트를 펼쳐 `_0`~`_23`을 전체 선택해 배열 칸에 드래그)
3. `Dim`, `Band BG`, `Band FG`, `Portrait`, `Mask` 칸에 자식 오브젝트를 연결합니다.

### 4-4. 호출

```csharp
public CutInPlayer cutIn;
public Sprite povertyLeaderPortrait;

void UseAttackCard(Card card)
{
    Action flash = PlayHitSfx;               // 0.3초: 타격음, 화면 흔들림 등
    Action fire  = () => card.Apply();       // 0.6초: 카드 효과 적용
    cutIn.OnFlash     += flash;
    cutIn.OnSkillFire += fire;
    cutIn.Play(povertyLeaderPortrait, onDone: () => {
        cutIn.OnFlash     -= flash;          // 다음 컷인에 누적되지 않도록 해제
        cutIn.OnSkillFire -= fire;
    });
}
```

- `pauseGameDuringCutIn`이 켜져 있으면 재생 동안 `Time.timeScale = 0`이 되고, 컷인은 `unscaledDeltaTime`으로 재생됩니다. 끝나면 원래 값으로 돌아갑니다.
- 이벤트는 `+=`로 붙일 때마다 누적되므로, 위처럼 끝날 때 `-=`로 해제하세요.

---

## 5. 포트레이트 준비

- 배경이 투명한 PNG를 권장합니다. 마스크가 빨간 띠 밖을 잘라 주므로 얼굴·눈 위주로 크게 크롭해도 됩니다.
- 띠 두께는 프레임 높이의 약 40%(960×540 기준 약 216px)이고, 띠는 **오른쪽 위로 11°** 기울어져 있습니다. 포트레이트도 같은 각도로 살짝 기울이면 잘 맞습니다.
- 포트레이트 위치·크기 변화는 `cutin_meta.json`의 `portrait` 배열에 프레임별로 들어 있습니다 (`x`, `y`는 띠 중심 기준 픽셀, y는 위쪽이 +). 스크립트는 이 값을 근사해서 움직입니다.
- 9–12프레임의 흔들림은 띠 시트에만 들어가 있습니다. 포트레이트도 같이 흔들고 싶으면 `OnFlash`에서 카메라를 흔드는 편이 간단합니다.

---

## 6. 수정·재생성

`make_cutin.py` 맨 위 상수를 바꾼 뒤 다시 실행하면 시트, meta, 미리보기가 모두 새로 만들어집니다.

```bash
pip install pillow numpy
python make_cutin.py 출력폴더
```

| 상수 | 의미 | 기본값 |
|---|---|---|
| `FW`, `FH` | 프레임 크기. 1920, 1080으로 바꾸면 원본 해상도 (시트가 7680×6480이 되므로 `COLS`/`ROWS` 조정 권장) | 960, 540 |
| `THETA` | 띠 각도 | -11° |
| `BASE_H` | 띠 두께 (화면 높이 비율) | 0.40 |
| `RED`, `DARK_RED` | 띠 색 / 줄무늬 색 | 빨강 |
| `FPS`, `N` | 재생 속도, 프레임 수 | 30, 24 |

교단별로 띠 색을 다르게 하고 싶다면 `RED`, `DARK_RED`만 바꿔 네 벌을 뽑으면 됩니다 (예: 순금의 형제단 = 금색, 털가죽 교단 = 초록).
