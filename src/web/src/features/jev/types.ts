import type { Source } from '../../shared/api/types'

// Mirrors src/BuildYourOwnAI.Api/Features/Jev/JevAsk.cs.

export type AssistantRef = { id: string; name: string; organizationName: string }

export type JevResponse =
  | { kind: 'answered'; assistant: AssistantRef; answer: string; found: boolean; sources: Source[]; alternatives: AssistantRef[] }
  | { kind: 'clarify'; candidates: AssistantRef[] }
  | { kind: 'noMatch' }
