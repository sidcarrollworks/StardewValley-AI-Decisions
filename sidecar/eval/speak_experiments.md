# Speak-question rewording experiment (2026-09-30)

## Model: typed-decisions (median 31 ms)

### Per state, per wording (P that the NPC has a line)

| state | baseline | wants-to-tell | bring-up | worth-telling-about | news-for-player | mention | share |
|---|---|---|---|---|---|---|---|
| gift-loved | 0.439 | 0.320 | 0.194 | 0.350 | 0.501 | 0.260 | 0.433 |
| quest-helped | 0.373 | 0.256 | 0.155 | 0.341 | 0.462 | 0.132 | 0.296 |
| festival-talked | 0.462 | 0.337 | 0.218 | 0.362 | 0.493 | 0.249 | 0.405 |
| saw-player | 0.432 | 0.379 | 0.216 | 0.278 | 0.485 | 0.218 | 0.372 |
| birthday-forgotten | 0.412 | 0.328 | 0.382 | 0.315 | 0.418 | 0.443 | 0.410 |
| dull-housemate | 0.400 | 0.295 | 0.181 | 0.340 | 0.510 | 0.211 | 0.384 |
| dull-no-news | 0.384 | 0.287 | 0.204 | 0.332 | 0.485 | 0.246 | 0.385 |

### Separation (newsy vs dull), sorted by best

| wording | mean newsy | mean dull | gap | newsy >= 0.5 |
|---|---|---|---|---|
| news-for-player | 0.472 | 0.497 | -0.026 | 1/5 |
| bring-up | 0.233 | 0.192 | 0.040 | 0/5 |
| wants-to-tell | 0.324 | 0.291 | 0.032 | 0/5 |
| baseline | 0.424 | 0.392 | 0.032 | 0/5 |
| mention | 0.260 | 0.229 | 0.031 | 0/5 |
| share | 0.383 | 0.384 | -0.001 | 0/5 |
| worth-telling-about | 0.329 | 0.336 | -0.006 | 0/5 |

## Model: english (median 30 ms)

### Per state, per wording (P that the NPC has a line)

| state | baseline | wants-to-tell | bring-up | worth-telling-about | news-for-player | mention | share |
|---|---|---|---|---|---|---|---|
| gift-loved | 0.221 | 0.132 | 0.090 | 0.114 | 0.285 | 0.123 | 0.189 |
| quest-helped | 0.177 | 0.128 | 0.065 | 0.066 | 0.707 | 0.051 | 0.095 |
| festival-talked | 0.328 | 0.178 | 0.081 | 0.169 | 0.488 | 0.121 | 0.229 |
| saw-player | 0.201 | 0.194 | 0.085 | 0.091 | 0.268 | 0.140 | 0.128 |
| birthday-forgotten | 0.279 | 0.143 | 0.183 | 0.310 | 0.259 | 0.143 | 0.198 |
| dull-housemate | 0.178 | 0.130 | 0.105 | 0.100 | 0.244 | 0.117 | 0.155 |
| dull-no-news | 0.160 | 0.122 | 0.110 | 0.091 | 0.198 | 0.120 | 0.145 |

### Separation (newsy vs dull), sorted by best

| wording | mean newsy | mean dull | gap | newsy >= 0.5 |
|---|---|---|---|---|
| news-for-player | 0.401 | 0.221 | 0.180 | 1/5 |
| baseline | 0.242 | 0.169 | 0.073 | 0/5 |
| worth-telling-about | 0.150 | 0.095 | 0.055 | 0/5 |
| wants-to-tell | 0.155 | 0.126 | 0.029 | 0/5 |
| share | 0.168 | 0.150 | 0.018 | 0/5 |
| mention | 0.115 | 0.118 | -0.003 | 0/5 |
| bring-up | 0.101 | 0.107 | -0.007 | 0/5 |

Note: the news bullets are the plain sentences the planner now sends (NewsPhrasing),
and the dull states are below MinNews 2.0, so in-game they never reach the model.
The speak threshold is now 0.25, a veto floor: the compressed answer band must not
randomly veto real news (a quest state measured 0.46-0.49 under the chosen wording).
