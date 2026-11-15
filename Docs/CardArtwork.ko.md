# 카드 이미지와 앞·뒷면 편집

모든 카드가 `Assets/Pilgrimage/UI/Card.prefab` 하나를 사용합니다. 손패와 중앙 공개 카드 모두 같은 프리팹입니다. 평소에는 일러스트 앞면을 표시하고, 마우스 오버 또는 방향키 선택 시 확대되면서 이름·비용·설명·플레이버 텍스트가 있는 뒷면으로 뒤집힙니다. 마우스가 벗어나거나 선택을 해제하면 앞면과 원래 크기로 돌아갑니다. 기존 우클릭/Space 상세보기, 더블클릭/Enter 사용은 유지했습니다.

## 이미지와 문구

- 이미지 매핑: `Assets/Pilgrimage/Cards/Resources/PilgrimageCardVisuals.asset`. 각 CardId에 Front/Back Sprite를 지정합니다.
- 일반 카드: 제공 텍스트 1~22번 = 이미지 번호 0~21번. CardId enum 숫자와 이미지 번호는 별개입니다.
- 교단 카드: 두 번째 ZIP의 파일명으로 연결했습니다. `이단 심문관.png`는 `이단 심판관 / Inquisitor`에 연결합니다.
- 교단 뒷면 및 상대 비공개 손패: 첫 번째 ZIP의 `46.png`(? / IGNOTUM) 이미지입니다. 상대 손패에는 카드 이름이나 실제 CardInstance를 연결하지 않습니다.
- `털가죽 집회`: 추가 제공한 `1222.png`를 앞면으로 연결했습니다.
- 문구: 실제 게임의 CardDefinition을 그대로 표시합니다. 원본 기획서는 `Docs/Pilgrimage_Game_Design_2026-11-15_Final.md`, 생성기는 `python3 Tools/import_card_design.py`입니다. 별도의 화면용 수치 사본은 없습니다.

뒷면의 Card Name, Cost, Card Description, Flavor Text는 프리팹에서 위치·폰트 크기·자동 크기 범위를 편집할 수 있습니다. CardView의 Hover Scale, Hover Lift, Flip Duration으로 확대 비율·위치·속도를 조절합니다. 원본 이미지와 프레임을 보존하기 위해 텍스트는 중앙의 빈 영역에 배치했습니다.

## 일반 카드 뒷면 원본 파일

뒷면 파일명은 순서대로 정렬되어 있지 않으므로 이미지 안의 번호/타로 이름으로 연결했습니다.

| 번호 | 카드 | 앞면 원본 | 뒷면 원본 |
|---:|---|---|---|
| 0 | 신도 | 0.png | 45.png |
| 1 | 헌금 요구 | 1.png | 33.png |
| 2 | 성직자 양성 | 2.png | 37.png |
| 3 | 포교 | 3.png | 40.png |
| 4 | 포고령 | 4.png | 36.png |
| 5 | 설파 | 5.png | 39.png |
| 6 | 신앙의 결속 | 6.png | 35.png |
| 7 | 공격적 선교 | 7.png | 38.png |
| 8 | 고행 | 8.png | 34.png |
| 9 | 고독한 성찰 | 9.png | 25.png |
| 10 | 대상단 투자 | 10.png | 29.png |
| 11 | 공의회 | 11.png | 32.png |
| 12 | 마녀사냥 | 12.png | 28.png |
| 13 | 추수꾼 | 13.png | 31.png |
| 14 | 정기 예배 | 14.png | 27.png |
| 15 | 이단 | 15.png | 30.png |
| 16 | 공개 비난 | 16.png | 26.png |
| 17 | 한 줌의 희망 | 17.png | 41.png |
| 18 | 거짓 계시 | 18.png | 43.png |
| 19 | 축복 | 19.png | 47.png |
| 20 | 그곳이 터져 죽습니다 | 20.png | 42.png |
| 21 | 낙원 | 21.png | 44.png |

전체 대응은 `Assets/Pilgrimage/Cards/CardArtSources.json`에도 기록했습니다. 원본 ZIP은 보존하고 UI용으로 최대 2048픽셀의 PNG 68장을 프로젝트에 넣었습니다.

`Pilgrimage → Cards → 카드 이미지 및 공통 프리팹 갱신`은 JSON 매핑에서 라이브러리를 다시 만들고 카드 프리팹의 앞·뒷면 레이아웃을 초기화하는 개발용 명령입니다. 일반적인 이미지 교체는 라이브러리의 Sprite 슬롯에서 하세요. 두 씬의 기존 UI 위치·크기는 변경하지 않고 공개 카드용 프리팹 인스턴스만 연결합니다.

## 규칙 변경

사용자 확인에 따라 제공한 텍스트로 카드 문구와 실제 비용·효과를 함께 갱신했습니다. 변경 전후 비교는 `Docs/CardTextDifferences.ko.md`, 현재 실행 규칙은 `Docs/FinalBalance_2026-11-15.ko.md`에 있습니다.

## 검증

실제 Unity 6000.6.9f1에서 EditMode **89개**, PlayMode **38개**가 모두 통과했습니다. 에디터 전용 코드를 제외한 Runtime/UI 컴파일과 기획서/45종 카탈로그 생성기 일치 검사도 통과했습니다.

![앞면](Images/CardArtwork.png)

![확대된 뒷면](Images/CardHover.png)
