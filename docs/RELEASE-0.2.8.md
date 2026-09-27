# 0.2.8

- Installer confirmation accepts `INSTALL` regardless of case/outer whitespace, retries empty/invalid input, and reports cancellation without a deployment exception. No installation or certificate changes happen before acceptance.
- The receiver retains a completed round through the next freeze time. Round kills, headshot-kill percentage, damage and net money display the previous round during freeze; current cash and match K/D/A stay live. On entering live play, round fields show the new round. Joining mid-freeze or losing continuity displays unknown instead of inventing prior statistics.
- Round finalization handles map round counters advancing during `over`, reset combat counters, and settlement cash arriving on entry to freeze. Purchases in the next freeze do not rewrite the saved summary.
- Damage increments use adjacent self-state counters. If the preceding local damage field was missing, a same-player, same-round GSI `previously.player.state.round_totaldmg` can provide the acknowledged baseline. Duplicate updates do not count twice. First packets, resets, different-player data and unknown current damage cannot generate damage numbers.
- Missing damage now says `GSI 未提供伤害`; a present counter without a comparable previous value says `缺少相邻基线`. This is update damage, never damage assigned to an individual victim. Separate damage and kill updates are not retrospectively attributed to each other.

Validation uses synthetic self-GSI data and the actual HTTP receiver, plus installation validation without trust or deployment changes. In-game and second-computer acceptance remain pending. The local snapshot inspected during diagnosis had no round damage; software cannot recover a value the game has not supplied.
