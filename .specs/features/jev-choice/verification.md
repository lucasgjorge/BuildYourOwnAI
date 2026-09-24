# Jev via primitiva de escolha verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: db01620..2d13fc7 (feature landed at f43f5a9 + 427a40c; round-1 FAIL at fe854c9; fix fe854c9..2d13fc7, excluding 66e482f which only adds `.specs/features/study-mode/plan.md`)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

Round 2 scope: the fix diff `fe854c9..2d13fc7` (`tests/.../CrossCuttingTests.cs`, `tests/.../RoutingChoiceTests.cs`; no production file of this feature changed) plus every non-PASS item from round 1 (AC 9 members for descriptions and router reply; C1 precision on option "2"). Proofs re-run in full at `2d13fc7`. Sections say whether they were verified at `2d13fc7` or carried from `fe854c9`. The real tree was not touched.

## Binding sources

Carried from `fe854c9`. No binding sources: the plan marks none. Step 1 did not apply, and the fix did not touch the interface.

## Selector mapping (rename in source-preview, door 2)

Verified at `2d13fc7`. The mapping is unchanged from round 1. Every mapped selector below appears as `Passed` in the full run at `2d13fc7`.

| Check | Old selector (checks.md) | Selector at HEAD |
| --- | --- | --- |
| C1 | `JevChoiceTests.Sends_question_and_one_option_per_assistant_plus_none` | `RoutingChoiceTests.Sends_question_and_one_option_per_assistant_plus_none` |
| C2 | `JevChoiceTests.Confident_choice_answers_with_alternatives_by_probability` | `RoutingChoiceTests.Confident_choice_answers_with_alternatives_by_probability` |
| C3 | `JevTests.No_match_records_gap_without_organization` · `OrganizationJevTests.No_match_records_gap_in_organization` | `RoutingTests.No_match_records_gap_without_organization` · `OrganizationRoutingTests.No_match_records_gap_in_organization` |
| C4 | `JevChoiceTests.Below_threshold_clarifies_by_probability` | `RoutingChoiceTests.Below_threshold_clarifies_by_probability` |
| C5 | `JevTests.Router_failure_falls_back_to_clarify` | `RoutingTests.Router_failure_falls_back_to_clarify` |
| C6 | `OpenRouterJevChoiceTests.Posts_choice_request_and_reads_principal_answer` | `OpenRouterRoutingChoiceTests.Posts_choice_request_and_reads_principal_answer` |
| C7 | `OpenRouterJevChoiceTests.Failure_throws_without_body_or_key` | `OpenRouterRoutingChoiceTests.Failure_throws_without_body_or_key` |
| C8 | `JevTests.Router_never_receives_instructions_or_documents` | `RoutingTests.Router_never_receives_instructions_or_documents` |
| C9 | `ObservabilityTests.Jev_and_gaps_never_log_content` | `ObservabilityTests.Routing_and_gaps_never_log_content` (in `CrossCuttingTests.cs`) |
| C10 | `UnconfiguredAiTests.Jev_without_router_key_falls_back_to_clarify` | `UnconfiguredAiTests.Routing_without_key_falls_back_to_clarify` |

The fix only added assertions: `RoutingChoiceTests.cs:54` (Nexora in option "2"), `CrossCuttingTests.cs:115-116,129,140` (routing secret, TEXT call, secret in the list), and the new `Timeout_comes_from_routing_config` (source-preview C19). No assertion was removed or weakened (`git diff fe854c9..2d13fc7 -- tests`).

## Checks

Verified at `2d13fc7`. Proof run, one invocation for the whole target (PowerShell, `DOCKER_HOST=npipe://./pipe/dockerDesktopLinuxEngine`): `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`, exit 0, 191 passed, 0 failed. Each named test below appears individually as `Passed`. C1 and C9 were re-judged in full. The other citations were refreshed where the fix moved lines in `RoutingChoiceTests.cs` (+3 usings, +1 assertion). Their judgment is carried from `fe854c9`.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | state = question; keys `1`,`2`,`nenhuma`; each option carries name, "Nexora", description | `RoutingChoiceTests.Sends_question_and_one_option_per_assistant_plus_none` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/RoutingChoiceTests.cs:49` - `Assert.Equal(["1", "2", "nenhuma"], sent!.Keys.Order())`; `:50-52` - `Assert.Contains("Culture"/"Nexora"/culture, sent["1"])`; `:53-55` - `Assert.Contains("RH", sent["2"])`, `Assert.Contains("Nexora", sent["2"])`, `Assert.Contains(hr, sent["2"])`; `:56` - `Assert.StartsWith(question + "\n", Factory.Router.PromptContaining(question))`. Round-1 precision note closed by `:54` (verified at `2d13fc7`) | PASS |
| C2 | choice "2" @0.9 -> answered B, alternatives [D, C, A] | `RoutingChoiceTests.Confident_choice_answers_with_alternatives_by_probability` Passed | `RoutingChoiceTests.cs:73` - `Assert.Equal("answered", ...kind)`; `:74` - `Assert.Equal(ids["B"], ...assistant.id)`; `:75` - `Assert.Equal([ids["D"], ids["C"], ids["A"]], ...alternatives ids)` (judgment carried from `fe854c9`) | PASS |
| C3 | "nenhuma" confident -> noMatch + gap (global and per org) | `RoutingTests.No_match_records_gap_without_organization` Passed; `OrganizationRoutingTests.No_match_records_gap_in_organization` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/RoutingTests.cs:129-132` - `Assert.Equal("noMatch", ...)`, `Assert.Equal(1, ScalarAsync("...organization_id is null and assistant_id is null"))`; `tests/BuildYourOwnAI.Api.Tests/Features/OrganizationRoutingTests.cs:80-83` - `Assert.Equal("noMatch", ...)`, `Assert.Equal(1, ScalarAsync("...organization_id = @o..."))` (files untouched by the fix; carried from `fe854c9`) | PASS |
| C4 | 0.59 -> clarify by probability, no chat call; 0.6 -> answered | `RoutingChoiceTests.Below_threshold_clarifies_by_probability(0.59/0.6)` both Passed | `RoutingChoiceTests.cs:80-81` - `[InlineData(0.59, "clarify")]`/`[InlineData(0.6, "answered")]`; `:95` - `Assert.Equal(kind, ...kind)`; `:98` - `Assert.Equal([ids["C"], ids["B"], ids["A"]], ...candidates)`; `:99` - `Assert.False(Factory.Chat.ReceivedCallContaining(question))` (judgment carried from `fe854c9`) | PASS |
| C5 | exception, "xyz", "0", "N+1" -> fallback clarify, up to 5 by name | `RoutingTests.Router_failure_falls_back_to_clarify(THROW/TEXT/0/7)` all Passed | `RoutingTests.cs:137-140` - `[InlineData("THROW")] [InlineData("TEXT")] [InlineData("0")] [InlineData("7")]`; `:153-154` - `Assert.Equal("clarify", ...)`, `Assert.Equal(["A", "B", "C", "D", "E"], ...candidates names)` (carried from `fe854c9`) | PASS |
| C6 | POST `https://openrouter.ai/api/v1/systemone`, Bearer, body shape; reads `answers.principal` | `OpenRouterRoutingChoiceTests.Posts_choice_request_and_reads_principal_answer` Passed | `RoutingChoiceTests.cs:192` - `Assert.Equal("https://openrouter.ai/api/v1/systemone", handler.Request.RequestUri!.ToString())`; `:193` - `Assert.Equal($"Bearer {Key}", ...Authorization)`; `:195-200` - model, state, `type == "choice"`, instructions, `criteria["2"]`; `:201-203` - `Choice == "1"`, `Confidence == 0.97`, `Probabilities["2"] == 0.02` (judgment carried from `fe854c9`) | PASS |
| C7 | 400 with marker and 200 without principal throw without body or key | `OpenRouterRoutingChoiceTests.Failure_throws_without_body_or_key(BadRequest/OK)` both Passed | `RoutingChoiceTests.cs:208-209` (both cases); `:212` - `Assert.ThrowsAnyAsync<Exception>`; `:215` - `Assert.DoesNotContain("body-marker-77c1", exception.ToString())`; `:216` - `Assert.DoesNotContain(Key, exception.ToString())` (judgment carried from `fe854c9`) | PASS |
| C8 | nothing sent to the choice contains the instructions or document marker | `RoutingTests.Router_never_receives_instructions_or_documents` Passed | `RoutingTests.cs:226` - `Assert.DoesNotContain(instructionsMarker, prompt)`; `:227` - `Assert.DoesNotContain(documentMarker, prompt)` (carried from `fe854c9`) | PASS |
| C9 | no log holds the question, the "Quando usar" description sent, or the router's reply (including an option it was not given) after answered, noMatch and invalid choice | `ObservabilityTests.Routing_and_gaps_never_log_content` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:115-116` - `routingSecret` placed in the assistant's `routingDescription`; `:125-126` answered (`Route(1)`) and noMatch (`Route("NONE")`); `:129` - `RouteAsync(..., FakeAiTriggers.Route("TEXT"))`, which makes the fake return `FakeRouterClient.OutputMarker` as the choice (`tests/BuildYourOwnAI.Api.Tests/Infrastructure/FakeAi.cs:156`); `:140-141` - `foreach (var secret in new[] { askSecret, routedSecret, noMatchSecret, gapAnswerSecret, routingSecret, FakeAiTriggers.FoundAnswer, FakeRouterClient.OutputMarker }) Assert.DoesNotContain(logged, text => text.Contains(secret))`. Both round-1 gaps closed, and both killed a fault (below). Verified at `2d13fc7` | PASS |
| C10 | no `AI:OpenRouter:ApiKey` -> fallback clarify | `UnconfiguredAiTests.Routing_without_key_falls_back_to_clarify` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/UnconfiguredAiTests.cs:54` - `Assert.Equal("clarify", ...kind)`; `:55` - `Assert.Equal(assistant, ...candidates[0].id)` (carried from `fe854c9`) | PASS |

## Coverage

The AC 9 row was recomputed at `2d13fc7`. The other rows are carried from `fe854c9`, because no production file changed (`git diff --stat fe854c9..2d13fc7 -- src/BuildYourOwnAI.Api` is empty).

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| AC 9 never-logged content (3: question, descriptions sent, router's reply), verified at `2d13fc7` | plan AC 9 (`plan.md:72`); log sites in `src/BuildYourOwnAI.Api/Features/Routing/RouteAsk.cs:129,151,160` | question C9 (`routedSecret`, `noMatchSecret`, `CrossCuttingTests.cs:125-126,140`) · descriptions sent C9 (`routingSecret` in `routingDescription`, `:115-116,140`; the description reaches `criteria` at `RouteAsk.cs:140`) · router's reply C9 (`Route("TEXT")` at `:129` yields `OutputMarker`, which is asserted at `:140`; reaches the invalid-key branch `RouteAsk.cs:158-161`) | - |
| decision outcomes (5), carried from `fe854c9` | `RouteAsk.cs:101-122` + `:156-162` | answered C2 · noMatch C3 · clarify below threshold C4 · fallback on exception C5 · fallback on unknown key C5 | - |
| threshold edges (2), carried | `RouteAsk.cs:171` | 0.59 C4 · 0.6 C4 | - |
| HTTP responses (3), carried | `src/BuildYourOwnAI.Api/Infrastructure/Ai/RoutingChoice.cs:37-42` | 2xx with principal C6 · non-2xx C7 · 2xx without principal C7 | - |
| doors (2), carried | plan Landing | 1: C6, C7, C10 · 2: C1, C2, C4 | - |
| startup config `IRoutingChoice` (2 assemblies), carried | `src/BuildYourOwnAI.Api/Program.cs:55` -> `AiServiceCollectionExtensions.cs:49-59`; `tests/.../Infrastructure/ApiFactory.cs:60-61` | Program/AddAi C10 · ApiFactory fake C1-C5, C8, C9 | - |
| option text members per option (3: name, organization, description) x 2 options, verified at `2d13fc7` | `RouteAsk.cs:140` | option "1": `RoutingChoiceTests.cs:50-52` · option "2": `:53-55` | - |
| fallback candidates cap (5), carried | `RouteAsk.cs:15,102` | C5 | - |
| alternatives/candidates cap (3), carried | `RouteAsk.cs:14,105,122` | C4, C2 | - |

## Test policy rows

Carried from `fe854c9`. The fix touched no file this row classifies (`RoutingChoice.cs` is unchanged), and its test lines only moved.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| HTTP client for an external provider (`OpenRouterJevChoice`, now `OpenRouterRoutingChoice`) | `src/BuildYourOwnAI.Api/Infrastructure/Ai/RoutingChoice.cs` | own level with a `HttpMessageHandler` stub: C6, C7 (`StubHandler` at `RoutingChoiceTests.cs:156`) | yes - route/method `:191-192`, header `:193`, body `:195-200`, response `:201-203`, non-2xx and missing `principal` `:208-216` |

Swept `existing` (authorization) is carried from `fe854c9`: `RouteAsk.cs` is unchanged since then.

## Faults injected

Verified at `2d13fc7`. Isolated in `git worktree add --detach <scratchpad>/wt HEAD`. The real tree's `git status --porcelain` was empty before and after. The worktree was removed and pruned. These are the surfaces the fix created or that were unproven in round 1. The round-1 faults (threshold, alternatives order, `nenhuma` option, route, exception body) are carried from `fe854c9`.

| Mutation | Location | Killed |
| --- | --- | --- |
| log the routing options (`logger.LogInformation("Routing options {Options}", string.Join(" / ", criteria.Values))`, separator shown as `/` here; the injected one was a pipe) | `src/BuildYourOwnAI.Api/Features/Routing/RouteAsk.cs:142` (after) | yes - `Routing_and_gaps_never_log_content`: `Assert.DoesNotContain() Failure: Filter matched in collection` |
| log the invalid choice key (`"Routing picked an option it was not given: {Choice}", choice.Choice`) | `RouteAsk.cs:160` | yes - `Routing_and_gaps_never_log_content`: `Assert.DoesNotContain() Failure: Filter matched in collection` |
| drop the organization from every option but the first (`i == 0 ? ... : $"{e.Name}: {e.RoutingDescription}"`) | `RouteAsk.cs:140` | yes - `Sends_question_and_one_option_per_assistant_plus_none`: `Sub-string not found: "Nexora"` at `RoutingChoiceTests.cs:54` |

A fourth fault in this worktree hardcoded the routing timeout (`RoutingChoice.cs:67`). It belongs to source-preview C19 and is reported there. The fifth, `scrollIntoView` in the preview, is also reported there.

## Gate

`dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` - 191 passed, 0 failed (exit 0) at 2d13fc7.

## Ranked gaps

None. Round-1 gaps 1 and 2 (AC 9 descriptions, router reply) are closed by `CrossCuttingTests.cs:115-116,129,140`, and both were shown to kill a fault. Precision gap 3 (Nexora in option "2") is closed by `RoutingChoiceTests.cs:54`.
