import * as React from 'react'
import { cn } from '@/lib/utils'

const Input = React.forwardRef<HTMLInputElement, React.InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...props }, ref) => {
    return (
      <input
        className={cn(
          'w-full px-2.5 py-[7px] bg-[var(--bg-input)] border border-[var(--border)] rounded-[var(--radius)] text-[var(--text)] font-[var(--font)] text-[10px] outline-none transition-colors placeholder:text-[var(--text-muted)] focus:border-[var(--border-bright)] disabled:opacity-30 disabled:cursor-not-allowed',
          className
        )}
        ref={ref}
        {...props}
      />
    )
  }
)
Input.displayName = 'Input'

export { Input }
