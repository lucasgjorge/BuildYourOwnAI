# Jev via primitiva de escolha verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: db01620..fe854c9 (feature landed at f43f5a9 + 427a40c; obligations verified at HEAD fe854c9, after the source-preview rename)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Verified at `fe854c9`. A previous Verifier run was cut off before fault injection and left no report; its scratch worktree (at 427a40c, holding a leftover mutant in `JevAsk.cs`) was removed during this run. The real tree was not touched.

## Binding sources

No binding sources: the plan marks none (`Sources` lists the `C:\reserve\aria` reference client and one real call, neither marked binding). Step 1 did not apply.

## Selector mapping (rename in source-preview, door 2)

Every proof in `checks.md` names a pre-rename selector. Each one maps to exactly one test at HEAD. Git confirms each old name at `427a40c`, and the assertion lines are unchanged apart from `Jev`->`Route` helper names and the model-id literal. The only other change is new assertions (see the diff below).

| Check | Old selector (checks.md) | Selector at HEAD |
| --- | --- | --- |
| C1 | `JevChoiceTests.Sends_question_and_one_option_per_assistant_plus_none` | `RoutingChoiceTests.Sends_question_and_one_option_per_assistant_plus_none` |
| C2 | `JevChoiceTests.Confident_choice_answers_with_alternatives_by_probability` | `RoutingChoiceTests.Confident_choice_answers_with_alternatives_by_probability` |
| C3 | `JevTests.No_match_records_gap_without_organization` · `OrganizationJevTests.No_match_records_gap_in_organization` | `RoutingTests.No_match_records_gap_without_organization` · `OrganizationRoutingTests.No_match_records_gap_in_organization` |
| C4 | `JevChoiceTests.Below_threshold_clarifies_by_probability` | `RoutingChoiceTests.Below_threshold_clarifies_by_probability` |
| C5 | `JevTests.Router_failure_falls_back_to_clarify` | `RoutingTests.Router_failure_falls_back_to_clarify` |
| C6 | `OpenRouterJevChoiceTests.Posts_choice_request_and_reads_principal_answer` | `OpenRouterRoutingChoiceTests.Posts_choice_request_and_reads_principal_answer` (this rename is not listed in source-preview/checks.md, which names only `JevTests`, `OrganizationJevTests`, `JevChoiceTests`) |
| C7 | `OpenRouterJevChoiceTests.Failure_throws_without_body_or_key` | `OpenRouterRoutingChoiceTests.Failure_throws_without_body_or_key` (same note) |
| C8 | `JevTests.Router_never_receives_instructions_or_documents` | `RoutingTests.Router_never_receives_instructions_or_documents` |
| C9 | `ObservabilityTests.Jev_and_gaps_never_log_content` | `ObservabilityTests.Routing_and_gaps_never_log_content` (in `CrossCuttingTests.cs`) |
| C10 | `UnconfiguredAiTests.Jev_without_router_key_falls_back_to_clarify` | `UnconfiguredAiTests.Routing_without_key_falls_back_to_clarify` |

No selector maps to nothing. Diffing the assertions from `427a40c` to HEAD shows the rename only added assertions (`Threshold_comes_from_routing_config`, and the 404 lines of `Old_jev_routes_are_gone`). None was removed or weakened.

## Checks

Proof run, one invocation for the whole target at HEAD (PowerShell, `DOCKER_HOST=npipe://./pipe/dockerDesktopLinuxEngine`): `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`, exit 0, 189 passed, 0 failed. The output lists each named test below as `Passed`.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | state = question; keys `1`,`2`,`nenhuma`; option text carries name, org, description | `RoutingChoiceTests.Sends_question_and_one_option_per_assistant_plus_none` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/RoutingChoiceTests.cs:47` - `Assert.Equal(["1", "2", "nenhuma"], sent!.Keys.Order())`; `:48-52` - `Assert.Contains("Culture"/"Nexora"/culture, sent["1"])`, `Assert.Contains("RH"/hr, sent["2"])`; `:53` - `Assert.StartsWith(question + "\n", Factory.Router.PromptContaining(question))`. Precision note: "Nexora" is asserted only in option `"1"`, not in `"2"` as the claim says (both come from the same format string, `RouteAsk.cs:140`) | PASS |
| C2 | choice "2" @0.9 -> answered B, alternatives [D, C, A] | `RoutingChoiceTests.Confident_choice_answers_with_alternatives_by_probability` Passed | `RoutingChoiceTests.cs:70` - `Assert.Equal("answered", ...kind)`; `:71` - `Assert.Equal(ids["B"], ...assistant.id)`; `:72` - `Assert.Equal([ids["D"], ids["C"], ids["A"]], ...alternatives ids)` | PASS |
| C3 | "nenhuma" confident -> noMatch + gap (global and per org) | `RoutingTests.No_match_records_gap_without_organization` Passed; `OrganizationRoutingTests.No_match_records_gap_in_organization` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/RoutingTests.cs:129-132` - `Assert.Equal("noMatch", ...)`, `Assert.Equal(1, ScalarAsync("...organization_id is null and assistant_id is null"))`; `tests/BuildYourOwnAI.Api.Tests/Features/OrganizationRoutingTests.cs:80-83` - `Assert.Equal("noMatch", ...)`, `Assert.Equal(1, ScalarAsync("...organization_id = @o..."))` | PASS |
| C4 | 0.59 -> clarify by probability, no chat call; 0.6 -> answered | `RoutingChoiceTests.Below_threshold_clarifies_by_probability(0.59/0.6)` both Passed | `RoutingChoiceTests.cs:92` - `Assert.Equal(kind, ...kind)` with `[InlineData(0.59, "clarify")]`/`[InlineData(0.6, "answered")]` at `:77-78`; `:95` - `Assert.Equal([ids["C"], ids["B"], ids["A"]], ...candidates)`; `:96` - `Assert.False(Factory.Chat.ReceivedCallContaining(question))` | PASS |
| C5 | exception, "xyz", "0", "N+1" -> fallback clarify, up to 5 by name | `RoutingTests.Router_failure_falls_back_to_clarify(THROW/TEXT/0/7)` all Passed | `RoutingTests.cs:137-140` - `[InlineData("THROW")] [InlineData("TEXT")] [InlineData("0")] [InlineData("7")]` (N = 6); `:153-154` - `Assert.Equal("clarify", ...)`, `Assert.Equal(["A", "B", "C", "D", "E"], ...candidates names)` | PASS |
| C6 | POST `https://openrouter.ai/api/v1/systemone`, Bearer, body shape; reads `answers.principal` | `OpenRouterRoutingChoiceTests.Posts_choice_request_and_reads_principal_answer` Passed | `RoutingChoiceTests.cs:171` - `Assert.Equal("https://openrouter.ai/api/v1/systemone", handler.Request.RequestUri!.ToString())`; `:172` - `Assert.Equal($"Bearer {Key}", ...Authorization)`; `:174-179` - model, state, `principal.type == "choice"`, instructions, `criteria["2"]`; `:180-182` - `choice.Choice == "1"`, `Confidence == 0.97`, `Probabilities["2"] == 0.02` | PASS |
| C7 | 400 with marker and 200 without principal throw without body or key | `OpenRouterRoutingChoiceTests.Failure_throws_without_body_or_key(BadRequest/OK)` both Passed | `RoutingChoiceTests.cs:187-188` (both cases); `:191` - `Assert.ThrowsAnyAsync<Exception>`; `:194` - `Assert.DoesNotContain("body-marker-77c1", exception.ToString())`; `:195` - `Assert.DoesNotContain(Key, exception.ToString())` | PASS |
| C8 | nothing sent to the choice contains the instructions or document marker | `RoutingTests.Router_never_receives_instructions_or_documents` Passed | `RoutingTests.cs:226` - `Assert.DoesNotContain(instructionsMarker, prompt)`; `:227` - `Assert.DoesNotContain(documentMarker, prompt)` | PASS |
| C9 | no log holds the question or the answers after answered and noMatch | `ObservabilityTests.Routing_and_gaps_never_log_content` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:137-138` - `foreach (var secret in new[] { askSecret, routedSecret, noMatchSecret, gapAnswerSecret, FakeAiTriggers.FoundAnswer, FakeRouterClient.OutputMarker }) Assert.DoesNotContain(logged, text => text.Contains(secret))`. Proves the check as written, but see Coverage: the check leaves out AC 9's "descrições enviadas" | PASS |
| C10 | no `AI:OpenRouter:ApiKey` -> fallback clarify | `UnconfiguredAiTests.Routing_without_key_falls_back_to_clarify` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/UnconfiguredAiTests.cs:54` - `Assert.Equal("clarify", ...kind)`; `:55` - `Assert.Equal(assistant, ...candidates[0].id)` | PASS |

## Coverage

Recomputed from the code (`RouteAsk.cs`, `RoutingChoice.cs`, `AiServiceCollectionExtensions.cs`) and from the plan's own enumerations (Landing doors, AC 9).

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| decision outcomes (5) | `src/BuildYourOwnAI.Api/Features/Routing/RouteAsk.cs:101-122` (null decision, not confident, chosen null, chosen) + `:156-162` (unknown key) | answered C2 · noMatch C3 · clarify below threshold C4 · fallback on exception C5 (THROW) · fallback on unknown key C5 (TEXT, 0, 7) | - |
| threshold edges (2) | `RouteAsk.cs:171` `choice.Confidence >= settings.ConfidenceThreshold` | 0.59 C4 · 0.6 C4 | - |
| HTTP responses (3) | `src/BuildYourOwnAI.Api/Infrastructure/Ai/RoutingChoice.cs:37-42` | 2xx with principal C6 · non-2xx C7 · 2xx without principal C7 | - |
| doors (2) | plan Landing | 1: C6, C7, C10 · 2: C1 (position keys + `nenhuma`), C2, C4 | - |
| startup config `IRoutingChoice` (2 assemblies) | read directly: `src/BuildYourOwnAI.Api/Program.cs:55` `builder.Services.AddAi(builder.Configuration)` -> `AiServiceCollectionExtensions.cs:49-59` (Unconfigured vs typed HttpClient); `tests/.../Infrastructure/ApiFactory.cs:60-61` replaces it with the fake | Program/AddAi unconfigured branch C10 · ApiFactory fake C1-C5, C8, C9 | - |
| AC 9 never-logged content (3: question, descriptions sent, router's answer) | plan AC 9 | question C9 (`routedSecret`, `noMatchSecret`) | descriptions sent: the test's routing description is the literal `"respostas curtas"` (`CrossCuttingTests.cs:114`) and is not in the secrets list at `:137`, so logging `criteria` would stay green. Router's answer: `FakeRouterClient.OutputMarker` is asserted at `:137`, but the fake only emits it for the `TEXT` trigger (`FakeAi.cs:156`), which this test never sends (`:125-126` use `Route(1)` and `Route("NONE")`), so that assertion is vacuous |
| fallback candidates cap (5 by name) | `RouteAsk.cs:15,102` | C5 asserts exactly A..E out of 6 | - |
| alternatives/candidates cap (3) | `RouteAsk.cs:14,105,122` | C4 (3 of 4), C2 (3 of 3 others) | - |

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| HTTP client for an external provider (`OpenRouterJevChoice`, now `OpenRouterRoutingChoice`) | `src/BuildYourOwnAI.Api/Infrastructure/Ai/RoutingChoice.cs` | one at its own level with a `HttpMessageHandler` stub: C6, C7 (`OpenRouterRoutingChoiceTests`, `StubHandler` at `RoutingChoiceTests.cs:135-146`) | yes - request route/method (`:170-171`), header (`:172`), body (`:174-179`), response reading (`:180-182`), each failure: non-2xx and missing `principal` (`:187-188`, `:191-195`) |

Swept `existing` re-read: authorization. The org scope and 404 are still at `RouteAsk.cs:84-85`, and the eligible query is still limited to the organization at `:87-89`. Present.

## Faults injected

Isolated in `git worktree add --detach <scratchpad>/wt2 HEAD`. The real tree's `git status --porcelain` was empty before and after, and the worktree was removed.

| Mutation | Location | Killed |
| --- | --- | --- |
| threshold `>=` -> `>` | `src/BuildYourOwnAI.Api/Features/Routing/RouteAsk.cs:171` | yes - `Below_threshold_clarifies_by_probability(0.6, "answered")` failed, `Assert.Equal() Failure: Strings differ` |
| alternatives ordered by name instead of probability (`OrderByDescending(p => p.Probability)` -> `OrderBy(p => p.Eligible.Name)`) | `RouteAsk.cs:168` | yes - `Confident_choice_answers_with_alternatives_by_probability`, `Collections differ` |
| drop the `"nenhuma"` option (`criteria[NoneKey] = NoneDescription;` removed) | `RouteAsk.cs:142` | yes - `Sends_question_and_one_option_per_assistant_plus_none`, `Collections differ` |
| POST to `"chat/completions"` instead of `"systemone"` | `src/BuildYourOwnAI.Api/Infrastructure/Ai/RoutingChoice.cs:35` | yes - `Posts_choice_request_and_reads_principal_answer`, `Strings differ` |
| response body appended to the non-2xx exception message | `RoutingChoice.cs:38` | yes - `Failure_throws_without_body_or_key(BadRequest)`, `Assert.DoesNotContain() Failure: Sub-string found` |

Not injected (cap of 5 reached): C3, C5, C8, C9, C10. The C9 gap above is shown by reading the code, not by a surviving mutant.

## Gate

`dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` - 189 passed, 0 failed (exit 0) at fe854c9.

## Ranked gaps

1. AC 9 "descrições enviadas" has no proof, because C9's claim dropped it from the AC. The routing description in the test is not a secret marker (`tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:114`, secrets list at `:137`). A handler that logged `criteria` would pass. Fix: give the routed assistant a secret routing description and add it to the list.
2. AC 9 "a resposta do Jev" (the router's output) is asserted only vacuously. `FakeRouterClient.OutputMarker` appears only under the `TEXT` trigger (`tests/.../Infrastructure/FakeAi.cs:156`), and `CrossCuttingTests.cs:125-126` never sends it. Fix: add a `TEXT`-routed call to the observability test.
3. Precision (not failing): C1 claims "Nexora" in option `"2"`, but only option `"1"` asserts it (`RoutingChoiceTests.cs:49`).
