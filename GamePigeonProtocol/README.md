macOS-only iMessage interceptor: polls ~/Library/Messages/chat.db for new messages (needs Full Disk Access) and replies via Messages.app AppleScript.
Run src/GamePigeonFucker; no private entitlement or native code required.

## GamePigeon protocol -- SOLVED (2026-09-06)

Full writeup of a since-deleted research effort (`../GamePigeonProtocol/`,
2582 captured messages across 18 games, `captures/FINDINGS.md`). Kept here
because this project is the natural place to actually send/receive
GamePigeon messages via Messages.app.

### Wire format

A GamePigeon `balloon_bundle_id` message's `payload_data` is an
NSKeyedArchiver plist. Resolve it (see `plistlib`/`NSKeyedUnarchiver`
approach) to get an `NSURL` under key `URL`, whose `NS.relative` string is:

    data:?ver=52&data=<SHUFFLED>

`<SHUFFLED>` is percent-escaped (only `%`, `=`, `&` need it -- see below for
why) and must be percent-*decoded once* before unshuffling.

### The shuffle

The unescaped `<SHUFFLED>` string is a **length-keyed character permutation**
of a plaintext query string. It is glibc's `rand48`/`drand48` PRNG, seeded by
`len(plaintext) * 0xef`, driving a **remove-and-reinsert derangement** (not a
textbook Fisher-Yates -- that's why guessing at "simple" permutation families
never found it):

```python
import math

class Rand48:
    def __init__(self):
        self.n = 0
    def srand(self, seed):
        seed &= 0xFFFFFFFF
        self.n = ((seed << 16) + 0x330E) & ((1 << 48) - 1)
    def next(self):
        self.n = (25214903917 * self.n + 11) & ((1 << 48) - 1)
        return self.n
    def drand(self):
        return self.next() / float(1 << 48)

def decrypt(blob):
    """Unshuffle a captured blob back to the (still percent-encoded)
    plaintext query string."""
    rand = Rand48()
    rand.srand(len(blob) * 0xEF)
    offsets, modifier = [], 0
    for _ in blob:
        offsets.append(int(math.floor(rand.drand() * (modifier + len(blob)))))
        modifier -= 1
    output = ""
    for i, offset in enumerate(reversed(offsets)):
        index = len(blob) - i - 1
        output = output[:offset] + blob[index] + output[offset:]
    return output
```

Then `urllib.parse.unquote()` the result once more (it's percent-encoded
twice: once for the inner query string's own values, once for embedding that
shuffled string in the outer URL) to get final plaintext like:

    ?sender=00BA5DB7-...&version=0&tver=5&ios=26.6.1&game=connect&id=...&
    size=4&player=1&player1=00BA5DB7-...&player2=62B18CB5-...&
    avatar1=body,1|eyes,4|...&avatar2=...&
    replay=board:1,0,0,...,0|move:0,2,1&num=4&build=...

Every message starts with `?` (it's the query-string prefix -- this is a
universal, corpus-wide invariant that held across all 2582 messages and was
the tell that the outer escaping model was right before the algorithm was
found). Board state for Four in a Row (`game=connect`) is a literal
`board:<42 comma cells, 0/1/2>|move:<x>,<y>,<player>` inside `replay` -- it
really is stored cell-by-cell, contrary to what the permuted-blob analysis
suggested.

### Origin of the algorithm (`Cryption.kt`)

Found in `github.com/OpenBubbles/OpenPigeon`, an open-source Android
reimplementation of GamePigeon (part of the OpenBubbles iMessage project)
that interoperates with real iPhones. Its `Game.kt` builds the plaintext via
`encodeQuery(message)` (a literal `key=value&key=value...` builder) and its
`Cryption.encrypt`/`decrypt` implement the shuffle above. Verified against
all 2582 captured messages across all 18 games: **100% decrypt to a
well-formed query string.**

To build/send a GamePigeon message from this project: construct the same
field map `Game.kt` uses (`sender`, `version`, `tver`, `ios`, `game`, `id`,
`player`, `player1`, `player2`, `avatar1`, `avatar2`, game-specific fields
like `replay`, `winner`, `num`, `build`, `caption`), URL-encode it into
`?key=value&...`, run the *encrypt* direction of the shuffle above (draw
`idx = floor(drand() * remaining.length)`, remove and append -- forward, not
reversed), percent-escape the result, and wrap as
`data:?ver=52&data=<encrypted>` inside the NSKeyedArchiver plist matching a
real GamePigeon message's structure (see `OpenPigeon`'s `MadridMessage`/
`Game.buildGameMessage` for the exact envelope).
