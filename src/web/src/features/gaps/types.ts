// Mirrors src/BuildYourOwnAI.Api/Features/Gaps/ListGaps.cs.

export type Ref = { id: string; name: string }

export type Gap = {
  id: string
  question: string
  askCount: number
  firstAskedAt: string
  lastAskedAt: string
  organization: Ref | null
  assistant: Ref | null
}
