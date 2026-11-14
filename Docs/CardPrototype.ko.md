# pilgrimage 카드 프로토타입 실행 안내

> 초기 프로토타입의 역사 자료입니다. 현재 공통 카드 데이터에서는 헌금 요구가 0코스트입니다. 최신 실행·설정·AI 안내는 [2026-11-14 개선 안내](AiUpdate_2026-11-14.ko.md)를 참조하세요.

이 구현은 카드 효과를 검증하는 첫 프로토타입입니다. 기존 SampleScene, 패키지, ProjectSettings는 변경하지 않습니다. Canvas나 EventSystem 없이 동작하는 Unity IMGUI 테스트 화면을 사용합니다. 실제 게임용 카드 그래픽, 애니메이션, AI, 승패 판정은 포함하지 않습니다.

## 1. 실행하기

1. Unity Hub에서 이 프로젝트를 **6000.6.9f1**로 엽니다.
2. 스크립트 가져오기와 컴파일이 끝날 때까지 기다립니다. Console에 빨간 오류가 없는지 확인합니다.
3. Project 창에서 `Assets/Scenes/SampleScene`을 더블클릭합니다.
4. 상단 메뉴 **GameObject → Pilgrimage → 카드 프로토타입 추가**를 선택합니다.
   - Hierarchy에 `Pilgrimage Card Prototype` 오브젝트가 생성됩니다.
   - 메뉴가 보이지 않으면 Hierarchy의 빈 곳을 우클릭 → Create Empty로 오브젝트를 만들고, Inspector의 Add Component에서 `Card Prototype Controller`를 검색하여 추가해도 됩니다.
5. **Ctrl+S**로 장면을 저장합니다. 장면 변경을 원치 않으면 Play 테스트 후 저장하지 않아도 됩니다.
6. 상단 **▶ Play**를 누르고 **Game** 탭을 선택합니다.
7. 손패의 **카드 사용** 버튼을 누릅니다. 수치, 카드 더미 개수와 최근 기록이 바뀝니다. 화면 아래가 보이지 않으면 스크롤합니다. Game 창은 1280×720 이상을 권장합니다.
8. **턴 종료 (임시)**는 다음 플레이어로 넘깁니다. **테스트 초기화**는 선택한 테스트 설정으로 모든 상태와 기록을 다시 만듭니다.

한 장면에는 테스트 컨트롤러를 하나만 두세요. 이 컴포넌트가 있는 장면에서만 테스트 UI가 표시됩니다. UI는 Play 중에 생성되므로 편집 상태의 Scene 창에서는 보이지 않습니다.

## 2. 카드별 확인 방법

기본 테스트 설정은 플레이어 2명, 각 헌금 5 / 신앙심 20 / 교리 정합성 20, 공용 무신론자 20입니다. 초기 손패는 포교·헌금 요구·정기 예배·신도 각 1장이고 덱은 8장입니다.

각 카드는 **테스트 초기화 후 하나씩** 사용하면 다음 결과를 확인할 수 있습니다.

| 사용 카드 | 헌금 | 신앙심 | 교리 정합성 | 공용 무신론자 | 카드 이동 |
|---|---:|---:|---:|---:|---|
| 포교 | 4 | 18 | 20 | 18 | 신도 2장 덱 추가, 사용한 포교는 버린 더미로 이동 |
| 헌금 요구 | 6 | 15 | 15 | 20 | 사용한 카드는 버린 더미로 이동 |
| 정기 예배 | 4 | 25 | 25 | 20 | 사용한 카드는 버린 더미로 이동 |
| [신도] | 5 | 20 | 20 | 20 | 사용 버튼 비활성화, 손패 유지 |

헌금 요구는 **비용 1을 지불할 수 있어야** 사용할 수 있습니다. 헌금 0에서 카드가 주는 헌금 2를 미리 받아 사용하는 것은 허용하지 않습니다. 헌금이 부족하면 버튼 옆에 이유가 표시되고 상태는 바뀌지 않습니다. 상대 플레이어의 자원과 카드 더미는 변경하지 않습니다.

**더미 내용 공개**를 켜면 모든 플레이어의 덱 순서와 손패를 볼 수 있습니다. 이 기능은 디버깅 전용입니다. 덱 목록의 왼쪽 카드부터 드로우하며, 포교로 추가된 신도는 현재 덱의 끝에 들어갑니다. 추가 위치도 테스트 구현의 가정입니다.

## 3. 임시 설정을 바꾸기

1. Play를 중지합니다.
2. Project 창에서 원하는 폴더(예: `Assets/Settings`)를 선택합니다.
3. 우클릭 → **Create → Pilgrimage → 임시 테스트 설정**을 선택합니다.
4. 만들어진 설정 에셋을 선택하고 Inspector의 **Values**를 펼칩니다.
5. Hierarchy의 `Pilgrimage Card Prototype`을 선택합니다.
6. 설정 에셋을 Inspector의 **Test Settings** 칸에 드래그합니다.
7. Play를 누릅니다. 설정을 연결하지 않으면 코드에 정의된 기본 테스트값을 사용합니다.

| Inspector 필드 | 기본값 | 의미 — 모두 임시 가정 |
|---|---|---|
| Player Count | 2 | 테스트 참가자 수. 최소 1명으로 처리 |
| Starting Offering | 5 | 플레이어별 시작 헌금. 음수 입력은 0으로 처리 |
| Starting Faith | 20 | 플레이어별 시작 신앙심 |
| Starting Doctrine | 20 | 플레이어별 시작 교리 정합성 |
| Starting Atheists | 20 | 공용 무신론자 수. 공용/개인 여부는 기획 확정 필요 |
| Clamp Effect Values At Zero | 꺼짐 | 꺼짐: 감소량을 그대로 적용하여 음수 허용. 켜짐: 효과 처리 후 수치를 최소 0으로 제한 |
| Initial Hand | 4장 | 각 플레이어의 초기 손패 목록 |
| Initial Deck | 8장 | 각 플레이어의 초기 덱 목록 |
| Max Cards Per Turn | 0 | 0은 사용 횟수 무제한, 양수는 턴당 사용 제한 |
| Draw On Turn Start | 1 | 턴 전환 후 다음 플레이어가 뽑는 카드 수 |
| Offering On Turn Start | 0 | 턴 전환 후 다음 플레이어에게 더할 헌금 |
| Discard Hand On Turn End | 꺼짐 | 켜면 턴 종료 시 남은 손패를 전부 버림 |
| Reshuffle Discard When Deck Empty | 켜짐 | 드로우 중 덱이 비면 자신의 버린 더미를 섞어 덱으로 사용 |
| Shuffle Initial Deck | 꺼짐 | 시작 덱을 섞을지 여부. 초기 손패는 섞지 않음 |
| Random Seed | 12345 | 같은 설정과 조작 순서에서 섞기 결과 재현 |

카드 선택 목록: `Evangelism`=포교, `RequestOffering`=헌금 요구, `RegularWorship`=정기 예배, `Follower`=신도.

**첫 턴에는 추가 드로우나 헌금 지급 없이 설정한 초기 손패·수치로 시작합니다.** 이후 턴 종료 버튼으로 넘어갈 때만 턴 시작 처리를 합니다. 턴 번호는 플레이어가 바뀔 때마다 1 증가하며 라운드 번호가 아닙니다. 손패 상한, 무신론자 부족 시 포교 제한, 헌금 자동 회복, 승패 조건은 확정 규칙으로 추가하지 않았습니다.

실행 중인 게임은 시작 시 설정을 복사합니다. Play 중 설정 에셋을 변경했다면 **테스트 초기화**를 눌러 다시 적용하세요. Play 중에도 ScriptableObject 에셋에 대한 Inspector 변경은 남을 수 있으므로, 설정 편집은 Play를 중지한 상태를 권장합니다.

비용 부족 검증은 Starting Offering을 0으로 바꿔 재실행하면 됩니다. 세 행동 카드 버튼이 모두 비활성화되고 헌금 부족 이유가 나와야 합니다.

## 4. 코드 위치

- `Assets/Scripts/Pilgrimage/Cards/CardDefinition.cs`: 확정된 카드 정의, 카드 종류, 개별 카드 식별자.
- `Assets/Scripts/Pilgrimage/Core/PlayerState.cs`: 플레이어 자원과 독립적인 덱·손패·버린 더미. 외부에서는 읽기 전용.
- `Assets/Scripts/Pilgrimage/Core/PrototypeGame.cs`: 사용 가능 여부 검증, 비용 지불, 효과 적용, 카드 이동, 임시 턴 처리.
- `Assets/Scripts/Pilgrimage/Prototype/PrototypeTestConfig.cs`: 미확정 초기값과 진행 규칙. Unity와 분리된 테스트 데이터.
- `Assets/Scripts/Pilgrimage/Prototype/PrototypeTestSettings.cs`: Inspector에서 편집할 수 있게 하는 ScriptableObject 에셋 타입.
- `Assets/Scripts/Pilgrimage/Prototype/CardPrototypeController.cs`: 테스트 버튼, 수치 표시, 최근 기록과 세션 초기화.
- `Assets/Scripts/Pilgrimage/Editor/CardPrototypeMenu.cs`: 장면에 컨트롤러를 추가하는 편의 메뉴.
- `Assets/Tests/EditMode/CardPrototypeTests.cs`: 카드 효과, 잘못된 사용, 카드 더미, 임시 턴 규칙에 대한 테스트.

런타임·에디터·테스트 코드는 각각 asmdef로 분리되어 에디터 도구와 테스트 코드가 게임 빌드에 들어가지 않습니다.

## 5. 테스트 및 한글 표시

Unity 메뉴 **Window → General → Test Runner**에서 **EditMode → Run All**로 자동 테스트를 실행할 수 있습니다. 카드 효과 3종, 신도 사용 차단, 비용 부족 시 상태 보존, 상대 턴/손패 밖/중복 사용 거부, 턴 전환, 사용 횟수 제한, 버린 더미 재섞기와 카드 보존, 하한 설정, 설정 복사, 난수 재현을 확인합니다.

Windows에서는 맑은 고딕, macOS에서는 Apple SD Gothic Neo 등 설치된 한글 폰트를 찾아 사용합니다. 한글이 네모로 보이면 한글을 지원하는 `.ttf`/`.otf` 폰트를 적절한 라이선스로 `Assets`에 가져오고 컨트롤러의 **Korean Font**에 지정하세요. 배포할 때는 OS 폰트에 의존하기보다 사용할 폰트 에셋을 연결하는 편이 좋습니다.

이 UI는 효과 검증용입니다. 게임 규칙이 확정되면 같은 데이터와 처리 코드를 유지하면서 실제 카드 화면으로 교체할 수 있습니다.
