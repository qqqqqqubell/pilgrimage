"""Regenerate immutable C# card data from the supplied UTF-8 design document.

Run: py Tools/import_card_design.py
Only the catalog data file is written. Original effect/flavor wording is retained.
Execution status and numeric payloads are explicit mappings reviewed in code.
"""
from pathlib import Path
import sys
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Docs/Pilgrimage_Game_Design_2026-11-15_Final.md'
DEST = ROOT / 'Assets/Scripts/Pilgrimage/Cards/CardCatalogData.cs'
IDS = {
    '신도': 'Follower', '포교': 'Evangelism', '헌금 요구': 'RequestOffering',
    '정기 예배': 'RegularWorship', '성직자 양성': 'ClergyTraining', '이단': 'Heretic',
    '공개 비난': 'PublicDenunciation', '공의회': 'Council', '추수꾼': 'Reaper',
    '공격적 선교': 'AggressiveMission', '빈민 구휼': 'PoorRelief',
    '구세군 모금': 'SalvationFundraising', '자선냄비': 'CharityPot',
    '누추한 성전': 'HumbleTemple', '십일조': 'Tithe', '이자': 'Interest',
    '면죄부': 'Indulgence', '성전 리모델링': 'TempleRemodeling', '대주교': 'Archbishop',
    '이단 심판관': 'Inquisitor', '성서무오론': 'Inerrancy', '고해성사': 'Confession',
    '신실한 신도': 'FaithfulFollower', '털갈이 의식': 'MoltingRitual',
    '깊은 숲의 성역': 'DeepForestSanctuary', '야성의 포효': 'WildRoar',
    '포고령': 'Edict', '설파': 'Preaching', '신앙의 결속': 'BondOfFaith',
    '고행': 'Penance', '고독한 성찰': 'SolitaryReflection', '대상단 투자': 'CaravanInvestment',
    '마녀사냥': 'WitchHunt', '한 줌의 희망': 'GlimmerOfHope', '거짓 계시': 'FalseRevelation',
    '축복': 'Blessing', '그곳이 터져 죽습니다': 'ReturnToFaith', '낙원': 'Paradise',
    '교단 간 고리대금업': 'InterfaithLoan', '대규모 선동': 'MassAgitation',
    '이단 심판': 'HeresyJudgment', '거부하는 신도': 'RefusingFollower',
    '털가죽의 부름': 'CallOfFur', '지역 정화': 'AreaCleansing', '털가죽 집회': 'FurRally',
}
PENDING = set()
BASIC = {
    'Evangelism': 'faithChange: -2, atheistChange: -2, followersToDeck: 2',
    'RequestOffering': 'offeringGain: 5, faithChange: -5, doctrineChange: -5',
    'RegularWorship': 'faithChange: 5, doctrineChange: 5',
    'Council': 'doctrineChange: 25', 'CharityPot': 'offeringGain: 1',
    'Tithe': 'offeringGain: 3, churchPropertyChange: 5',
    'Inerrancy': 'doctrineChange: 15', 'Confession': 'faithChange: 20, doctrineChange: 20',
    'Indulgence': 'offeringGain: 40, churchPropertyChange: 30, faithChange: -5, doctrineChange: -5',
    'Preaching': 'doctrineChange: 10', 'BondOfFaith': 'faithChange: 15, doctrineChange: 15',
    'GlimmerOfHope': 'offeringGain: 10', 'ReturnToFaith': 'faithChange: 10',
    'Paradise': 'faithChange: 20, doctrineChange: 20',
    'SolitaryReflection': 'faithChange: 10, doctrineChange: 10',
    'HumbleTemple': 'faithChange: 15, doctrineChange: 15',
    'WitchHunt': 'offeringGain: 20',
}
TARGETS = {'PublicDenunciation', 'Reaper', 'AggressiveMission', 'PoorRelief', 'BondOfFaith', 'FalseRevelation',
           'InterfaithLoan', 'HeresyJudgment', 'CallOfFur'}
SPECIAL = {'Preaching', 'BondOfFaith', 'GlimmerOfHope', 'ReturnToFaith', 'Paradise', 'Indulgence',
           'HumbleTemple', 'TempleRemodeling', 'Archbishop', 'Penance', 'SolitaryReflection', 'CaravanInvestment', 'WitchHunt'}


def quoted(value):
    return json.dumps(value, ensure_ascii=False)


def main():
    faction = 'None'
    rows = []
    for line in SOURCE.read_text(encoding='utf-8-sig').splitlines():
        line = line.strip()
        if line.startswith('###'):
            if '청빈의 사도단' in line: faction = 'Poverty'
            elif '순금의 형제단' in line: faction = 'Gold'
            elif '절대 교율 교단' in line: faction = 'Discipline'
            elif '털가죽 교단' in line: faction = 'Fur'
        if not re.match(r'^(?:- |\d+\. )', line) or not ('｜' in line or '|' in line): continue
        plain = re.sub(r'^(?:- |\d+\. )', '', line).replace('**', '')
        content, flavor = re.split(r'[｜|]', plain, maxsplit=1)
        match = re.match(r'\s*(.+?)(?:\(((?:헌금|현금) [^)]+)\))?\s*[:：]\s*(.*)', content)
        if not match or match[1].strip() not in IDS: continue
        name, cost_text, effect = match[1].strip(), match[2], match[3].strip()
        card_id = IDS[name]
        cost = int(cost_text.split()[-1]) if cost_text and cost_text.split()[-1].isdigit() else -1
        category = 'Follower' if card_id == 'Follower' else 'Heretic' if card_id == 'Heretic' else 'General' if faction == 'None' else 'Faction'
        passive = card_id in {'Follower', 'FaithfulFollower'}
        status = 'NotImplemented' if card_id in PENDING else 'Implemented'
        conditions = '사용 불가' if passive else '자신의 신앙심이 40 이상' if card_id == 'WildRoar' else '원문에 추가 사용 조건 없음'
        if card_id == 'Evangelism': conditions = '무신론자가 2명 이상 남았을 때 발동할 수 있습니다.'
        if card_id == 'GlimmerOfHope': conditions = '헌금이 10 이하일 때만 발동할 수 있습니다.'
        if card_id == 'Interest': conditions = '한 턴에 한 개만 사용 가능합니다.'
        if card_id == 'InterfaithLoan': conditions = '헌금이 10 이상일 때만 발동할 수 있습니다.'
        if card_id == 'MoltingRitual': conditions = '자신의 덱에 신도 카드 2장 이상이 있을 때 발동할 수 있습니다.'
        if card_id == 'Paradise': conditions = '자신의 신도수가 20명 이상일 때 발동할 수 있습니다.'
        args = [f'CardId.{card_id}', quoted(name), quoted(effect), str(cost), str(not passive and card_id not in PENDING).lower(),
                f'category: CardCategory.{category}', f'faction: FactionId.{faction}',
                f'implementation: EffectImplementation.{status}', f'conditions: {quoted(conditions)}',
                f'flavorText: {quoted(flavor.strip())}', f'costText: {quoted("" if passive else (cost_text or "원문 미명시").replace("현금", "헌금"))}',
                f'costKind: CardCostKind.{"None" if passive else "Variable" if cost_text == "헌금 X" else "Fixed" if cost >= 0 else "MissingSpecification"}',
                f'effectKey: {quoted("pending" if card_id in PENDING else card_id if card_id in SPECIAL else "basic" if card_id in BASIC or passive else card_id)}']
        if card_id in BASIC: args.append(BASIC[card_id])
        if card_id in TARGETS: args.append('target: TargetKind.Player')
        if card_id in {'Edict', 'FurRally'}: args.append('target: TargetKind.Card')
        if card_id == 'FaithfulFollower': args.extend(['countsAsFollower: true', 'protectedFromCardEffects: true'])
        if card_id == 'RefusingFollower': args.append('countsAsFollower: true')
        if card_id == 'TempleRemodeling': args.append('churchPropertyChange: -5')
        rows.append(f'            {{ CardId.{card_id}, new CardDefinition(' + ',\n                '.join(args) + ') },')
    assert len(rows) == 45, f'Expected 45 cards; parsed {len(rows)}'
    output = '// Generated by Tools/import_card_design.py from Docs/Pilgrimage_Game_Design_2026-11-15_Final.md\nusing System.Collections.Generic;\n\nnamespace Pilgrimage\n{\n    internal static class CardCatalogData\n    {\n        internal static Dictionary<CardId, CardDefinition> Create() => new Dictionary<CardId, CardDefinition>\n        {\n'
    output += '\n'.join(rows) + '\n        };\n    }\n}\n'
    if '--check' in sys.argv:
        assert DEST.read_text(encoding='utf-8') == output, 'Card catalog differs from latest design/importer. Regenerate it.'
    else:
        DEST.write_text(output, encoding='utf-8')
    print(f'Registered {len(rows)} cards. Unimplemented effects: {len(PENDING)}.')


if __name__ == '__main__': main()
