import * as React from 'react'
import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-1.5 whitespace-nowrap transition-all disabled:pointer-events-none disabled:opacity-30 cursor-pointer',
  {
    variants: {
      variant: {
        default:
          'bg-[var(--bg-input)] border border-[var(--border)] text-[var(--text-muted)] hover:border-[var(--border-bright)] hover:text-[var(--text)] font-[var(--font)] text-[10px] font-semibold uppercase tracking-[0.05em] rounded-[var(--radius)]',
        ghost:
          'bg-transparent border-none text-[var(--text-muted)] hover:text-[var(--text)] hover:bg-[rgba(255,255,255,0.05)] font-[var(--font)] text-[10px] rounded-[var(--radius)]',
        danger:
          'bg-[var(--bg-input)] border border-[var(--border)] text-[var(--text-muted)] hover:border-[var(--error)] hover:text-[var(--error)] font-[var(--font)] text-[10px] rounded-[var(--radius)]',
        save:
          'w-full bg-[var(--bg-input)] border border-[var(--border)] text-[var(--text)] hover:border-[var(--border-bright)] hover:bg-[var(--bg-elevated)] font-[var(--font)] text-[11px] font-bold uppercase tracking-[0.05em] rounded-[var(--radius)]',
        add:
          'bg-transparent border border-[var(--border)] text-[var(--text-muted)] hover:border-[var(--border-bright)] hover:text-[var(--text)] font-[var(--font)] text-[9px] font-semibold rounded-[var(--radius)]',
      },
      size: {
        default: 'h-8 px-3 py-1',
        sm: 'h-6 px-2 py-0.5',
        icon: 'h-8 w-8',
        lg: 'h-[42px] px-4',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  }
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button'
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        {...props}
      />
    )
  }
)
Button.displayName = 'Button'

export { Button, buttonVariants }
