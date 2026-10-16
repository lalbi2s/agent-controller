import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'inline-flex items-center rounded-sm px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.5px] transition-colors',
  {
    variants: {
      variant: {
        default:
          'bg-[var(--bg-elevated)] text-[var(--text-muted)] border border-[var(--border)]',
        success:
          'bg-[rgba(74,222,128,0.1)] text-[var(--success)] border border-[rgba(74,222,128,0.2)]',
        warning:
          'bg-[rgba(251,191,36,0.1)] text-[var(--warning)] border border-[rgba(251,191,36,0.2)]',
        error:
          'bg-[rgba(248,113,113,0.1)] text-[var(--error)] border border-[rgba(248,113,113,0.2)]',
        outline: 'border border-[var(--border)] text-[var(--text-muted)] bg-transparent',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props} />
}

export { Badge, badgeVariants }
