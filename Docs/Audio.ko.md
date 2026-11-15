# BGM / SFX

오디오는 클라이언트마다 하나의 `Pilgrimage Audio` 오브젝트에서 로컬로 재생합니다. 메뉴 진입 전에 시작하며 씬 전환·게임 초기화·결과 화면에서도 BGM을 유지합니다. 기존 GameplayVisualCoordinator의 AudioSource와 이벤트/컷인 훅을 재사용하며 게임 규칙이나 네트워크 메시지는 변경하지 않습니다.

## 조절

- 초기 설정과 재생 순서: `Assets/Audio/Resources/PilgrimageAudioSettings.asset`.
- 기본 순서: Ritual Pulse → Ah of the Desert → Before the First Bell → 반복. Playlist 배열 순서를 바꾸면 적용됩니다.
- BGM: 기본 볼륨 0.45, 곡 전환 1.5초 크로스페이드. 컷인 동안 평소의 35%로 낮춘 뒤 복원합니다. 스트리밍으로 가져옵니다.
- SFX: 기본 볼륨 0.8. `Assets/Resources/sfx/`의 OGG 50개를 이름의 숫자 접미사를 제거해 그룹화하고 그룹 안에서 무작위로 선택합니다. Decompress On Load로 가져옵니다.
- 실행 중 Hierarchy의 `Pilgrimage Audio`에서 Bgm Volume / Sfx Volume을 각각 조절할 수 있습니다. 코드에서는 `PilgrimageAudio.Instance.BgmVolume` / `SfxVolume`을 사용합니다. 사용자 볼륨은 PlayerPrefs에 저장되며 초기 설정값보다 우선합니다.
- 같은 그룹은 기본 0.07초 간격, 동시 2개까지, 전체는 기본 12개 음성으로 제한합니다. 오디오 난수는 게임 난수와 분리했습니다.

## 연결

선택 ui_hover / 뒤집기 card_flip / 드로우 card_draw / 성공한 사용 card_play / 카드 추가 card_place / 보상 card_reward / 헌금 coin_gain·coin_spend / 공격 및 피해 hit_heavy·hit_slash·debuff / 강화 buff·buff_sparkle / 이단화 heresy / 라운드 round_start / 차례 turn_change / 결과 jingle_win·jingle_lose.

컷인 시작 시 BGM을 낮추고 whoosh, 기존 Flash 타이밍에 impact를 재생합니다. 취소·종료 시에도 BGM 볼륨이 복원됩니다. 싱글플레이와 LAN은 기존 공개 연출 이벤트의 중복 제거와 큐를 그대로 사용합니다. 호스트·클라이언트 모두 자신의 기기에서 재생하며 오디오 RPC를 추가하지 않습니다.

첨부 참고 자료는 `Assets/Audio/Documentation/`에 보존했습니다. 참고 C#은 중복 컴포넌트 생성을 피하기 위해 `.cs.txt`로 저장했습니다. 런타임/오디오 임포터 컴파일만 확인하며 테스트 실행과 Git 커밋·푸시는 하지 않습니다.
