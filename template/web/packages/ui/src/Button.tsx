import type { ComponentPropsWithoutRef } from "react";

import { cn } from "./class-names";

export type ButtonProps = ComponentPropsWithoutRef<"button"> & {
  variant?: "primary" | "secondary";
};

const variantClasses = {
  primary:
    "border-slate-900 bg-slate-900 text-white hover:bg-slate-700 focus-visible:ring-slate-500",
  secondary:
    "border-slate-300 bg-white text-slate-900 hover:bg-slate-100 focus-visible:ring-slate-400",
};

export function Button({
  className,
  variant = "primary",
  type = "button",
  ...props
}: ButtonProps) {
  return (
    <button
      className={cn(
        "inline-flex min-h-10 items-center justify-center rounded-md border px-4 py-2 text-sm font-medium transition-colors",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2",
        "disabled:pointer-events-none disabled:opacity-50",
        variantClasses[variant],
        className,
      )}
      type={type}
      {...props}
    />
  );
}
