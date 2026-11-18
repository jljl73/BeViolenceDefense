import csv, gzip, json, sys
from collections import defaultdict

def analyze(path):
    opener = gzip.open if path.endswith('.gz') else open
    first = {}; ticks = defaultdict(list)
    with opener(path, 'rt') as f:
        for row in csv.DictReader(f):
            tick = int(row['tick']); ticks[tick].append(row)
            if int(row['attacks']) > 0: first.setdefault(int(row['id']), tick)
    contact = min(first.values())
    window = [rows for tick,rows in ticks.items() if contact <= tick <= contact + 300]
    result = {'first_contact_tick':contact,
        'first_attack_ticks':first,
        'attackers_in_first_5_seconds':sorted(i for i,t in first.items() if t <= contact + 300),
        'last_first_attack_seconds_after_contact':(max(first.values()) - contact)/60}
    if 'canAttack' in window[0][0]:
        result['maximum_attack_ready_in_first_5_seconds'] = max(sum(int(r['canAttack']) for r in rows) for rows in window)
    return result

for path in sys.argv[1:]: print(path, json.dumps(analyze(path), indent=2))
