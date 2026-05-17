import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";

interface Props {
  icon?: LucideIcon;
  title: string;
  description?: string;
  action?: ReactNode;
}

export function EmptyState({ icon: Icon, title, description, action }: Props) {
  return (
    <Card>
      <CardContent className="py-14 text-center space-y-3">
        {Icon && <Icon className="h-12 w-12 text-muted-foreground mx-auto" />}
        <p className="font-medium">{title}</p>
        {description && <p className="text-sm text-muted-foreground max-w-md mx-auto">{description}</p>}
        {action && <div className="pt-2 flex justify-center">{action}</div>}
      </CardContent>
    </Card>
  );
}
