# Reflection decision trial

Requested Laya model: `typed-decisions`. These are controlled hypothetical scenes, not a simulated town.

Calls: 15; explicit fallbacks: 0; non-Laya answers: 0; incomplete submitted packets: 0; server-truncated responses: 0; unverified server usage: 0.

Each pair ran baseline → variant → exact baseline repeat. Total variation (TV) is half the sum of absolute probability differences: 0 means unchanged, 1 means disjoint distributions. ID TV compares the same labels; semantic TV follows the same line meaning across swapped labels. Repeat TV describes observed inference variability over one repeat; it is not a statistical confidence bound. Top-choice ties use label order, independent of submitted candidate order.
For the label-renaming control, ID TV is mechanically 1 because all labels changed; semantic TV is the meaningful comparison.

| Probe | ID TV | Semantic TV | Repeat TV | Top baseline → variant → repeat |
|---|---:|---:|---:|---|
| memory | 0.0687 | 0.0687 | 0.0000 | o3 → o3 → o3 |
| trust | 0.0379 | 0.0379 | 0.0000 | o3 → o3 → o3 |
| line-meaning | 0.1148 | 0.1031 | 0.0000 | o3 → o3 → o3 |
| choice-order | 0.0330 | 0.0330 | 0.0000 | o3 → o3 → o3 |
| choice-labels | 1.0000 | 0.0643 | 0.0000 | o3 → r9 → o3 |

These measurements describe sensitivity to the controlled edits. They do not establish realism, character quality, or whether a town is interesting. A preferred answer is not prescribed.

## memory

Only remembered kindness changes to remembered hostility.

Thought held fixed: I could offer Pam a hand, but perhaps I should first say what is on my mind.

Baseline memory: Pam praised Penny's work yesterday, when nobody else was around.
Variant memory: Pam mocked Penny's work yesterday, when nobody else was around.
Baseline character context: Penny is reserved, sensitive to public embarrassment, and wants to be heard without losing her relationship with Pam.

| Sample | Backend | ms | Semantic text chars | Full memory / context / lines / thought |
|---|---|---:|---:|---|
| baseline | authored+laya | 94.2780 | 733 | yes / yes / yes / yes |
| variant | authored+laya | 29.8429 | 732 | yes / yes / yes / yes |
| baseline-repeat | authored+laya | 30.0682 | 733 | yes / yes / yes / yes |

| Sample | Routed model | Input tokens | Dropped state tokens | Server truncated | Truncated questions |
|---|---|---:|---:|---|---|
| baseline | typed-decisions | 197 | 0 | NO |  |
| variant | typed-decisions | 198 | 0 | NO |  |
| baseline-repeat | typed-decisions | 197 | 0 | NO |  |

| ID | Baseline line | Variant line | P(base) | P(variant) | P(repeat) | Δ by ID |
|---|---|---|---:|---:|---:|---:|
| o1 | What happened still bothers me. Could we talk privately? | What happened still bothers me. Could we talk privately? | 0.1965 | 0.2476 | 0.1965 | 0.0511 |
| o2 | Everyone should hear how you treated me. Explain yourself. | Everyone should hear how you treated me. Explain yourself. | 0.1848 | 0.2023 | 0.1848 | 0.0175 |
| o3 | Let me give you a hand with that. | Let me give you a hand with that. | 0.4023 | 0.3508 | 0.4023 | -0.0514 |
| o4 | I need more time to think. | I need more time to think. | 0.1306 | 0.1292 | 0.1306 | -0.0014 |
| o5 | No. I don't want to act on this thought. | No. I don't want to act on this thought. | 0.0859 | 0.0700 | 0.0859 | -0.0159 |

## trust

Only Penny's expectation of trust changes; memory, thought and lines stay fixed.

Thought held fixed: I could offer Pam a hand, but perhaps I should first say what is on my mind.

Baseline memory: Pam spoke sharply to Penny yesterday and later asked whether they could talk.
Baseline character context: Penny trusts Pam to listen in private and keep her confidence. Penny is reserved and wants to be heard.
Variant character context: Penny distrusts Pam to listen in private or keep her confidence. Penny is reserved and wants to be heard.

| Sample | Backend | ms | Semantic text chars | Full memory / context / lines / thought |
|---|---|---:|---:|---|
| baseline | authored+laya | 30.2160 | 732 | yes / yes / yes / yes |
| variant | authored+laya | 26.0232 | 734 | yes / yes / yes / yes |
| baseline-repeat | authored+laya | 20.2668 | 732 | yes / yes / yes / yes |

| Sample | Routed model | Input tokens | Dropped state tokens | Server truncated | Truncated questions |
|---|---|---:|---:|---|---|
| baseline | typed-decisions | 198 | 0 | NO |  |
| variant | typed-decisions | 199 | 0 | NO |  |
| baseline-repeat | typed-decisions | 198 | 0 | NO |  |

| ID | Baseline line | Variant line | P(base) | P(variant) | P(repeat) | Δ by ID |
|---|---|---|---:|---:|---:|---:|
| o1 | What happened still bothers me. Could we talk privately? | What happened still bothers me. Could we talk privately? | 0.2942 | 0.2614 | 0.2942 | -0.0328 |
| o2 | Everyone should hear how you treated me. Explain yourself. | Everyone should hear how you treated me. Explain yourself. | 0.2076 | 0.2218 | 0.2076 | 0.0142 |
| o3 | Let me give you a hand with that. | Let me give you a hand with that. | 0.3186 | 0.3404 | 0.3186 | 0.0218 |
| o4 | I need more time to think. | I need more time to think. | 0.1053 | 0.1071 | 0.1053 | 0.0018 |
| o5 | No. I don't want to act on this thought. | No. I don't want to act on this thought. | 0.0743 | 0.0692 | 0.0743 | -0.0051 |

## line-meaning

Only the two confrontation lines trade places between stable IDs; both keep the same act kind.

Thought held fixed: I could offer Pam a hand, but perhaps I should first say what is on my mind.

Baseline memory: Pam praised Penny's work yesterday, when nobody else was around.
Baseline character context: Penny is reserved, sensitive to public embarrassment, and wants to be heard without losing her relationship with Pam.

| Sample | Backend | ms | Semantic text chars | Full memory / context / lines / thought |
|---|---|---:|---:|---|
| baseline | authored+laya | 20.6676 | 733 | yes / yes / yes / yes |
| variant | authored+laya | 19.2750 | 733 | yes / yes / yes / yes |
| baseline-repeat | authored+laya | 22.4815 | 733 | yes / yes / yes / yes |

| Sample | Routed model | Input tokens | Dropped state tokens | Server truncated | Truncated questions |
|---|---|---:|---:|---|---|
| baseline | typed-decisions | 197 | 0 | NO |  |
| variant | typed-decisions | 197 | 0 | NO |  |
| baseline-repeat | typed-decisions | 197 | 0 | NO |  |

| ID | Baseline line | Variant line | P(base) | P(variant) | P(repeat) | Δ by ID |
|---|---|---|---:|---:|---:|---:|
| o1 | What happened still bothers me. Could we talk privately? | Everyone should hear how you treated me. Explain yourself. | 0.1965 | 0.1460 | 0.1965 | -0.0505 |
| o2 | Everyone should hear how you treated me. Explain yourself. | What happened still bothers me. Could we talk privately? | 0.1848 | 0.2980 | 0.1848 | 0.1132 |
| o3 | Let me give you a hand with that. | Let me give you a hand with that. | 0.4023 | 0.3480 | 0.4023 | -0.0543 |
| o4 | I need more time to think. | I need more time to think. | 0.1306 | 0.1205 | 0.1306 | -0.0101 |
| o5 | No. I don't want to act on this thought. | No. I don't want to act on this thought. | 0.0859 | 0.0875 | 0.0859 | 0.0016 |

Semantic alignment (same actual line):

| Base ID → variant ID | Line | P(base) | P(variant at mapped ID) | Δ semantic |
|---|---|---:|---:|---:|
| o1 → o2 | What happened still bothers me. Could we talk privately? | 0.1965 | 0.2980 | 0.1015 |
| o2 → o1 | Everyone should hear how you treated me. Explain yourself. | 0.1848 | 0.1460 | -0.0388 |
| o3 → o3 | Let me give you a hand with that. | 0.4023 | 0.3480 | -0.0543 |
| o4 → o4 | I need more time to think. | 0.1306 | 0.1205 | -0.0101 |
| o5 → o5 | No. I don't want to act on this thought. | 0.0859 | 0.0875 | 0.0016 |

## choice-order

Only candidate order reverses; IDs, lines, thought and proposed choice stay fixed.

Thought held fixed: I could offer Pam a hand, but perhaps I should first say what is on my mind.

Baseline memory: Pam praised Penny's work yesterday, when nobody else was around.
Baseline character context: Penny is reserved, sensitive to public embarrassment, and wants to be heard without losing her relationship with Pam.

| Sample | Backend | ms | Semantic text chars | Full memory / context / lines / thought |
|---|---|---:|---:|---|
| baseline | authored+laya | 21.9061 | 733 | yes / yes / yes / yes |
| variant | authored+laya | 19.1065 | 733 | yes / yes / yes / yes |
| baseline-repeat | authored+laya | 19.6774 | 733 | yes / yes / yes / yes |

| Sample | Routed model | Input tokens | Dropped state tokens | Server truncated | Truncated questions |
|---|---|---:|---:|---|---|
| baseline | typed-decisions | 197 | 0 | NO |  |
| variant | typed-decisions | 197 | 0 | NO |  |
| baseline-repeat | typed-decisions | 197 | 0 | NO |  |

| ID | Baseline line | Variant line | P(base) | P(variant) | P(repeat) | Δ by ID |
|---|---|---|---:|---:|---:|---:|
| o1 | What happened still bothers me. Could we talk privately? | What happened still bothers me. Could we talk privately? | 0.1965 | 0.1990 | 0.1965 | 0.0025 |
| o2 | Everyone should hear how you treated me. Explain yourself. | Everyone should hear how you treated me. Explain yourself. | 0.1848 | 0.1678 | 0.1848 | -0.0170 |
| o3 | Let me give you a hand with that. | Let me give you a hand with that. | 0.4023 | 0.4162 | 0.4023 | 0.0139 |
| o4 | I need more time to think. | I need more time to think. | 0.1306 | 0.1471 | 0.1306 | 0.0165 |
| o5 | No. I don't want to act on this thought. | No. I don't want to act on this thought. | 0.0859 | 0.0699 | 0.0859 | -0.0160 |

## choice-labels

Only opaque choice labels change; actual lines, order and kinds stay fixed. The proposal reference is renamed to the same semantic option.

Thought held fixed: I could offer Pam a hand, but perhaps I should first say what is on my mind.

Baseline memory: Pam praised Penny's work yesterday, when nobody else was around.
Baseline character context: Penny is reserved, sensitive to public embarrassment, and wants to be heard without losing her relationship with Pam.

| Sample | Backend | ms | Semantic text chars | Full memory / context / lines / thought |
|---|---|---:|---:|---|
| baseline | authored+laya | 19.0189 | 733 | yes / yes / yes / yes |
| variant | authored+laya | 19.7285 | 733 | yes / yes / yes / yes |
| baseline-repeat | authored+laya | 20.7233 | 733 | yes / yes / yes / yes |

| Sample | Routed model | Input tokens | Dropped state tokens | Server truncated | Truncated questions |
|---|---|---:|---:|---|---|
| baseline | typed-decisions | 197 | 0 | NO |  |
| variant | typed-decisions | 197 | 0 | NO |  |
| baseline-repeat | typed-decisions | 197 | 0 | NO |  |

| ID | Baseline line | Variant line | P(base) | P(variant) | P(repeat) | Δ by ID |
|---|---|---|---:|---:|---:|---:|
| o1 | What happened still bothers me. Could we talk privately? | (ID absent) | 0.1965 | 0.0000 | 0.1965 | -0.1965 |
| o2 | Everyone should hear how you treated me. Explain yourself. | (ID absent) | 0.1848 | 0.0000 | 0.1848 | -0.1848 |
| o3 | Let me give you a hand with that. | (ID absent) | 0.4023 | 0.0000 | 0.4023 | -0.4023 |
| o4 | I need more time to think. | (ID absent) | 0.1306 | 0.0000 | 0.1306 | -0.1306 |
| o5 | No. I don't want to act on this thought. | (ID absent) | 0.0859 | 0.0000 | 0.0859 | -0.0859 |
| r7 | (ID absent) | What happened still bothers me. Could we talk privately? | 0.0000 | 0.1699 | 0.0000 | 0.1699 |
| r2 | (ID absent) | Everyone should hear how you treated me. Explain yourself. | 0.0000 | 0.1853 | 0.0000 | 0.1853 |
| r9 | (ID absent) | Let me give you a hand with that. | 0.0000 | 0.3645 | 0.0000 | 0.3645 |
| r1 | (ID absent) | I need more time to think. | 0.0000 | 0.1574 | 0.0000 | 0.1574 |
| r4 | (ID absent) | No. I don't want to act on this thought. | 0.0000 | 0.1229 | 0.0000 | 0.1229 |

Semantic alignment (same actual line):

| Base ID → variant ID | Line | P(base) | P(variant at mapped ID) | Δ semantic |
|---|---|---:|---:|---:|
| o1 → r7 | What happened still bothers me. Could we talk privately? | 0.1965 | 0.1699 | -0.0266 |
| o2 → r2 | Everyone should hear how you treated me. Explain yourself. | 0.1848 | 0.1853 | 0.0005 |
| o3 → r9 | Let me give you a hand with that. | 0.4023 | 0.3645 | -0.0378 |
| o4 → r1 | I need more time to think. | 0.1306 | 0.1574 | 0.0268 |
| o5 → r4 | No. I don't want to act on this thought. | 0.0859 | 0.1229 | 0.0370 |

Full request, response, submitted-prompt receipts, normalized weights and timings are in the accompanying JSON file. Generation was disabled for every probe.
