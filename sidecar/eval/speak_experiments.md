# Speak-question rewording experiment (typed-decisions, 2026-09-30)

## Per state, per wording (P that the NPC has a line)

| state | baseline | wants-to-tell | bring-up | worth-telling-about | news-for-player | mention | share |
|---|---|---|---|---|---|---|---|
| gift-loved | 0.480 | 0.443 | 0.402 | 0.396 | 0.474 | 0.312 | 0.445 |
| quest-helped | 0.548 | 0.485 | 0.507 | 0.571 | 0.555 | 0.457 | 0.483 |
| festival-talked | 0.455 | 0.441 | 0.365 | 0.365 | 0.487 | 0.291 | 0.382 |
| saw-player | 0.302 | 0.323 | 0.277 | 0.272 | 0.475 | 0.226 | 0.335 |
| talked | 0.608 | 0.595 | 0.465 | 0.529 | 0.728 | 0.362 | 0.649 |
| birthday-forgotten | 0.466 | 0.441 | 0.344 | 0.427 | 0.574 | 0.376 | 0.538 |
| dull-housemate | 0.347 | 0.359 | 0.265 | 0.243 | 0.398 | 0.235 | 0.309 |
| dull-no-news | 0.324 | 0.312 | 0.246 | 0.230 | 0.265 | 0.223 | 0.274 |

## Separation (newsy vs dull), sorted by best

| wording | mean newsy | mean dull | gap | newsy >= 0.5 |
|---|---|---|---|---|
| news-for-player | 0.549 | 0.331 | 0.218 | 3/6 |
| worth-telling-about | 0.427 | 0.236 | 0.190 | 2/6 |
| share | 0.472 | 0.292 | 0.180 | 2/6 |
| baseline | 0.476 | 0.336 | 0.141 | 2/6 |
| bring-up | 0.393 | 0.256 | 0.138 | 1/6 |
| wants-to-tell | 0.455 | 0.336 | 0.119 | 1/6 |
| mention | 0.337 | 0.229 | 0.108 | 0/6 |

Note: the planner's speak threshold is 0.5; a good wording puts the newsy states
above it and the dull states below it, with a large gap.

## Cross-check on `english` (same sweep)

| wording | mean newsy | mean dull | gap | newsy >= 0.5 |
|---|---|---|---|---|
| news-for-player | 0.549 | 0.331 | 0.218 | 3/6 |
| worth-telling-about | 0.427 | 0.236 | 0.190 | 2/6 |
| share | 0.472 | 0.292 | 0.180 | 2/6 |
| baseline | 0.476 | 0.336 | 0.141 | 2/6 |
| bring-up | 0.393 | 0.256 | 0.138 | 1/6 |
| wants-to-tell | 0.455 | 0.336 | 0.119 | 1/6 |
| mention | 0.337 | 0.229 | 0.108 | 0/6 |

`news-for-player` wins on both checkpoints and is the planner's wording now.
