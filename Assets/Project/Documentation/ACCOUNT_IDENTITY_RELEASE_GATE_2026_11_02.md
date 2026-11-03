# Identity / Version Gate — P0 No-Go Preflight

This ZIP is not a gameplay fix. It introduces a reproducible source-only negative check, so unimplemented P0 work cannot be accidentally marked PASS. It does not mutate Firebase, invoke Unity, modify GitHub or the existing source.

The current source must first be fixed in existing AccountService, PlatformRuntime/Steam returning sign-in, identity-bound UI/cache flows, Firebase RTDB rules and RTDB lobby transport. Changes must be made as complete project-relative original files in a separate functional build ZIP, then validated on a demo Auth+RTDB emulator. Do not test actual Steam A/B and Guest conversion expecting success from this ZIP.

Node invocation (from Backend/Firebase):
    node qa/phase19bl_identity_release_preflight.test.cjs

Expected current result: BLOCKED (exit code 2). A future green static check is only a candidate, not runtime PASS.
