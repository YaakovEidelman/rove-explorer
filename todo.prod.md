# TODO (production)

Rules for agents: this file is human-owned, same as TODO.md. Do NOT add, expand, reword, or
"improve" any item here. Only edit it when explicitly told to. As each item is finished, delete
that item and renumber the rest. When the last item is done, delete this entire file.

The following is in no particular order (other than that the first group is blocking, within a
group no particular order)

## Blocking for a real release
1. The release pipeline only produces one rolling "dev" prerelease tag, deleted and recreated on
   every push to main. No versioned/stable channel, no changelog, no "last known-good build."
2. The Windows binary is not code signed, so first run likely trips SmartScreen/Defender.
