import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export function PlaceholderPage({ title, description }: { title: string; description?: string }) {
  return (
    <Card className="max-w-3xl mx-auto">
      <CardHeader><CardTitle>{title}</CardTitle></CardHeader>
      <CardContent className="text-muted-foreground">
        {description ?? "This module is scaffolded. UI will be built out in the next iteration."}
      </CardContent>
    </Card>
  );
}
