import { useMutation } from '@tanstack/react-query'
import { postJson } from '../../shared/api/client'
import type { StudyAnswer, StudySession } from './types'

export function useCreateStudySession(organizationId: string) {
  return useMutation({
    mutationFn: (request: { documentIds: string[]; questionCount: number }) =>
      postJson<StudySession>(`/api/organizations/${organizationId}/study-sessions`, request),
  })
}

export function useAnswerStudyQuestion() {
  return useMutation({
    mutationFn: ({ sessionId, questionId, option }: { sessionId: string; questionId: string; option: number }) =>
      postJson<StudyAnswer>(`/api/study-sessions/${sessionId}/questions/${questionId}/answer`, { option }),
  })
}
