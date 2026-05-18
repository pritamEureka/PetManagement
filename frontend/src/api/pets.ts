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

export const petsApi = {
  mine: () => api.get<Pet[]>("/pets/mine").then((r) => r.data),
  get: (id: string) => api.get<Pet>(`/pets/${id}`).then((r) => r.data),
  create: (data: PetInput) => api.post<Pet>("/pets", data).then((r) => r.data),
  update: (id: string, data: PetInput) => api.put(`/pets/${id}`, data),
  remove: (id: string) => api.delete(`/pets/${id}`)
};
