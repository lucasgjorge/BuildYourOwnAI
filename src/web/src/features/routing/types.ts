import type { Source } from '../../shared/api/types'

// Mirrors src/BuildYourOwnAI.Api/Features/Routing/RouteAsk.cs.

export type AssistantRef = { id: string; name: string; organizationName: string }

/** The thread's last answered turn: context that lets a follow-up be routed. */
export type PreviousTurn = { question: string; assistantId: string }

export type RoutingRequest = { question: string; previous?: PreviousTurn }

export type RoutingResponse =
  | { kind: 'answered'; assistant: AssistantRef; answer: string; found: boolean; sources: Source[]; alternatives: AssistantRef[] }
  | { kind: 'clarify'; candidates: AssistantRef[] }
  | { kind: 'noMatch' }
