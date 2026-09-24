// Mirrors src/BuildYourOwnAI.Api/Features/Study/{CreateStudySession,AnswerStudyQuestion}.cs.

export type StudyQuestion = { id: string; position: number; prompt: string; options: string[] }

export type StudySession = { id: string; createdAt: string; questions: StudyQuestion[] }

export type StudyAnswer = {
  correct: boolean
  chosenOption: number
  correctOption: number
  explanation: string
  source: { documentId: string; fileName: string; chunkIndex: number }
}
