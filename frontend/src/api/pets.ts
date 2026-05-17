import { api } from "./client";
import type { PetInput } from "@/lib/schemas";

export interface Pet {
  id: string;
  name: string;
  animalType: string;
  breed?: string | null;
  gender: string;
  birthDate?: string | null;
  weightKg?: number | null;
  color?: string | null;
  tagNumber?: string | null;
  primaryPhotoUrl?: string | null;
  allergies?: string | null;
  dietNotes?: string | null;
  isAvailableForAdoption: boolean;
  createdAt: string;
}

const unwrap = <T,>(p: Promise<{ data: { data: T } }>) => p.then((r) => r.data.data);

export const petsApi = {
  mine: () => unwrap<Pet[]>(api.get("/pets/mine")),
  get: (id: string) => unwrap<Pet>(api.get(`/pets/${id}`)),
  create: (data: PetInput) => unwrap<Pet>(api.post("/pets", data)),
  update: (id: string, data: PetInput) => api.put(`/pets/${id}`, data),
  remove: (id: string) => api.delete(`/pets/${id}`)
};
