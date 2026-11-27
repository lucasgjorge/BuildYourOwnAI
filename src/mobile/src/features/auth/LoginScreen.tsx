// Mirrors src/web/src/features/auth/{LoginPage,AuthForm}.tsx.
import { Redirect, router } from 'expo-router'
import { useState } from 'react'
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native'
import { SafeAreaView } from 'react-native-safe-area-context'
import { errorTitle } from '../../shared/api/client'
import { useSession } from '../../shared/session'
import { colors, fonts } from '../../shared/theme'
import { Alert, Button, TextField } from '../../shared/ui'
import { useLogin } from './api'

export function LoginScreen() {
  const session = useSession()
  const login = useLogin()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  if (session.data) return <Redirect href="/organizations" />

  const incomplete = email.trim() === '' || password === ''
  const submit = () => {
    if (incomplete || login.isPending) return
    login.mutate({ email: email.trim(), password }, { onSuccess: () => router.replace('/organizations') })
  }

  return (
    <SafeAreaView style={styles.screen}>
      <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
          <Text style={styles.brand}>BuildYourOwnAI</Text>
          <Text role="heading" style={styles.title}>Entrar</Text>
          <View style={styles.card}>
            <TextField
              label="E-mail"
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              autoComplete="email"
              autoCorrect={false}
              keyboardType="email-address"
            />
            <TextField
              label="Senha"
              value={password}
              onChangeText={setPassword}
              onSubmitEditing={submit}
              autoComplete="current-password"
              secureTextEntry
            />
            {login.error != null && <Alert>{errorTitle(login.error)}</Alert>}
            <Button
              title={login.isPending ? 'Processando...' : 'Entrar'}
              onPress={submit}
              disabled={login.isPending || incomplete}
            />
          </View>
          {/* PENDING: becomes a link once the "Criar conta" screen exists (check C8). */}
          <Text style={styles.footer}>Não tem conta? Crie na versão web.</Text>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  )
}

const styles = StyleSheet.create({
  screen: { backgroundColor: colors.fog, flex: 1 },
  flex: { flex: 1 },
  content: { gap: 16, padding: 24, paddingTop: 48 },
  brand: { color: colors.ink, fontFamily: fonts.display, fontSize: 18 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 30, marginTop: 24 },
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.line,
    borderRadius: 12,
    borderWidth: 1,
    gap: 16,
    padding: 20,
  },
  footer: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14 },
})
