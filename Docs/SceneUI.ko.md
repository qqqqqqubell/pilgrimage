# 씬에서 편집하는 UI

`Assets/Scenes/PilgrimageDemo.unity`와 `SampleScene.unity`는 Play 전부터 Canvas와 전체 화면 UI를 포함합니다. 기존 SampleScene의 IMGUI 프로토타입 화면도 이 Canvas 화면으로 전환했습니다.

## 에셋 대입

1. Hierarchy에서 `Pilgrimage Demo → Pilgrimage v2 Canvas`를 펼칩니다.
2. UI 오브젝트의 **Image → Source Image**에 Sprite를 넣습니다. 위치와 크기는 RectTransform에서 편집합니다.
3. 카드의 공통 외형은 `Assets/Pilgrimage/UI/Card.prefab`, 뒷면은 `CardBack.prefab`, 제거 대상 카드 행은 `CardChoice.prefab`에서 편집합니다.
4. 다른 화면을 편집하려면 현재 표시 중인 `Game Mode Selection`을 끄고 `Faction Selection`, `LAN Lobby`, `Detail Overlay`, `Development Card Laboratory`, `Edict Removal Selection`, `Reward Selection` 중 원하는 화면을 켭니다. Play를 누르면 시작 화면 상태로 돌아갑니다.

일반 UI의 기본 외형은 흰색 사각형과 어두운 글자이며, 카드는 제공한 일러스트와 뒷면 이미지를 사용합니다. [카드 편집 안내](CardArtwork.ko.md)를 참고하세요. 배치된 Image의 Sprite와 RectTransform은 시작 시 다시 지정하지 않습니다. 숫자·카드 이름 등 게임 데이터와 버튼 동작은 실행 중 갱신됩니다. 오브젝트 이름과 부모 구조는 코드의 버튼 연결에 사용하므로 유지하세요.

실험실 카드 목록, LAN 자리·준비·배정 버튼, 보상 결과, 컷인 레이어와 상태 변화/효과 슬롯도 씬에 배치되어 있습니다. 검색 시 목록을 삭제하지 않고 숨겨 재사용합니다. 손패·상대 뒷면·제거 대상 카드만 카드 프리팹을 복제합니다. 손패의 위치·회전과 연출 위치·투명도는 게임 진행에 맞춰 움직입니다.

`DemoController`의 **Presentation Assets**는 선택 사항입니다. 비워 두면 프로젝트의 PilgrimageVisualAssets 라이브러리를 불러와 컷인과 타격·헌금 연출을 재생합니다. 카드별 앞·뒷면 이미지는 `PilgrimageCardVisuals` 라이브러리에서 지정합니다.

**Pilgrimage → UI → 기본 씬 UI 다시 생성**은 개발용 초기화 명령입니다. 두 씬과 세 카드 프리팹의 편집 내용을 초기화하므로, 일반적인 에셋 교체에는 사용하지 마세요.
