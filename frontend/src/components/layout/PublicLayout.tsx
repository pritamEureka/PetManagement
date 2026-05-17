import { Link, Outlet } from "react-router-dom";
import { PawPrint } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ThemeToggle } from "@/components/theme/ThemeToggle";

export function PublicLayout() {
  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b bg-background/80 backdrop-blur sticky top-0 z-40">
        <div className="container flex items-center justify-between h-16">
          <Link to="/" className="flex items-center gap-2 font-bold text-lg">
            <PawPrint className="h-5 w-5 text-primary" /> Pawzaroo
          </Link>
          <nav className="hidden md:flex items-center gap-6 text-sm">
            <Link to="/#features" className="text-muted-foreground hover:text-foreground">Features</Link>
            <Link to="/#vets" className="text-muted-foreground hover:text-foreground">Find a vet</Link>
            <Link to="/#adoption" className="text-muted-foreground hover:text-foreground">Adopt</Link>
            <Link to="/#marketplace" className="text-muted-foreground hover:text-foreground">Marketplace</Link>
          </nav>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <Button variant="ghost" asChild><Link to="/login">Sign in</Link></Button>
            <Button asChild><Link to="/register">Get started</Link></Button>
          </div>
        </div>
      </header>
      <main className="flex-1"><Outlet /></main>
      <footer className="border-t bg-card/40">
        <div className="container py-6 text-sm text-muted-foreground flex flex-col md:flex-row md:items-center md:justify-between gap-2">
          <p>&copy; {new Date().getFullYear()} Pawzaroo. All paws reserved.</p>
          <div className="flex gap-4">
            <Link to="/privacy" className="hover:text-foreground">Privacy</Link>
            <Link to="/terms" className="hover:text-foreground">Terms</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
