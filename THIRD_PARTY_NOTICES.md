# Third-party notices

## Open Source Free Realms Launcher

This repository contains an adapted copy of the Open Source Free Realms launcher from:

https://github.com/Open-Source-Free-Realms/Launcher

Its GNU Affero General Public License text is included in `OSFR-Launcher/LICENSE`.

## Discord Game SDK

The launcher includes Discord's precompiled Linux x86_64 Game SDK library for Rich Presence:

```text
OSFR-Launcher/src/Discord/lib/x86_64/discord_game_sdk.so
SHA-256: ed22f6755eff063893f3a0fe3fad02b1ddf260fba1c3064d3fe3046b350c75d8
```

Source/vendor: Discord Game SDK, distributed by Discord at https://discord.com/developers/docs/developer-tools/game-sdk

This native binary is not built from this repository's source. When it is updated, maintainers must update the hash above and record the vendor SDK version in the same commit. Discord's component remains subject to Discord's applicable SDK terms.
