# 0.2.9

Includes all [0.2.8 fixes](RELEASE-0.2.8.md) for installer confirmation, previous-round statistics and damage baselines, plus mouse trail stability changes.

- Losing Game Bar focus, changing foreground/pinned mode, or releasing local pointer capture no longer clears the independently running background Raw Input trail. Hiding/suspending the widget, entering edit mode and disabling background input still clear it.
- A transient mouse-state read failure preserves a still-fresh baseline; an actual receiver session change starts a new trail.
- Twelve retained paths now use fixed sample-time groups and continuous opacity. Aging no longer migrates geometry between opacity bands every frame. Adding points to a group does not brighten its older samples.
- Geometry updates only when samples, expiry, projection or lifetime changes. Fixed viewport size, adaptive downscaling, bounded history and idle recentering remain intact. Local pointer baselines are cleared when idle recentering occurs on the animation timer.

Validation: 111 core checks, including stable geometry during aging, continuous opacity, no brightness restart, extreme-motion bounds, reset and time-group wraparound. Game Bar/in-game visual acceptance remains pending; automated tests do not prove that every host compositor configuration is flicker-free.
