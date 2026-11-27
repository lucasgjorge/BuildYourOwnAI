// Mirrors src/web/src/shared/ui.tsx.
import { Pressable, StyleSheet, Text, TextInput, View, type TextInputProps } from 'react-native'
import { colors, fonts } from './theme'

type ButtonProps = {
  title: string
  onPress: () => void
  disabled?: boolean
  tone?: 'primary' | 'quiet' | 'danger'
}

export function Button({ title, onPress, disabled = false, tone = 'primary' }: ButtonProps) {
  return (
    <Pressable
      role="button"
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      style={[styles.button, styles[tone], disabled && styles.disabled]}
    >
      <Text style={[styles.buttonText, styles[`${tone}Text`]]}>{title}</Text>
    </Pressable>
  )
}

export function Alert({ children }: { children: string }) {
  return (
    <View role="alert" style={styles.alert}>
      <Text style={styles.alertText}>{children}</Text>
    </View>
  )
}

export function TextField({ label, ...props }: TextInputProps & { label: string }) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <TextInput accessibilityLabel={label} placeholderTextColor={colors.muted} style={styles.input} {...props} />
    </View>
  )
}

const styles = StyleSheet.create({
  button: { alignItems: 'center', borderRadius: 6, borderWidth: 1, paddingHorizontal: 16, paddingVertical: 12 },
  buttonText: { fontFamily: fonts.sansMedium, fontSize: 15 },
  primary: { backgroundColor: colors.ink, borderColor: colors.ink },
  primaryText: { color: colors.surface },
  quiet: { backgroundColor: colors.surface, borderColor: colors.line },
  quietText: { color: colors.ink },
  danger: { backgroundColor: colors.surface, borderColor: colors.danger },
  dangerText: { color: colors.danger },
  disabled: { opacity: 0.5 },
  alert: { backgroundColor: colors['danger-soft'], borderRadius: 6, padding: 12 },
  alertText: { color: colors.danger, fontFamily: fonts.sans, fontSize: 14 },
  field: { gap: 4 },
  label: { color: colors.ink, fontFamily: fonts.sansMedium, fontSize: 14 },
  input: {
    backgroundColor: colors.surface,
    borderColor: colors.line,
    borderRadius: 6,
    borderWidth: 1,
    color: colors.ink,
    fontFamily: fonts.sans,
    fontSize: 16,
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
})
