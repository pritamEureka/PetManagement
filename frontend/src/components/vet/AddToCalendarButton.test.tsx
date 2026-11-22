import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AddToCalendarButton } from "./AddToCalendarButton";
import { calendarApi } from "@/api/vetPlus";

vi.mock("@/api/vetPlus", () => ({
  calendarApi: {
    downloadAppointment: vi.fn().mockResolvedValue(undefined),
    downloadUpcoming: vi.fn().mockResolvedValue(undefined)
  }
}));

describe("AddToCalendarButton", () => {
  beforeEach(() => vi.clearAllMocks());

  it("downloads a single appointment when appointmentId is given", async () => {
    render(<AddToCalendarButton appointmentId="appt-1" />);
    await userEvent.click(screen.getByRole("button", { name: /add to calendar/i }));

    await waitFor(() => expect(calendarApi.downloadAppointment).toHaveBeenCalledWith("appt-1"));
    expect(calendarApi.downloadUpcoming).not.toHaveBeenCalled();
  });

  it("exports all upcoming appointments when no id is given", async () => {
    render(<AddToCalendarButton label="Export all (.ics)" />);
    await userEvent.click(screen.getByRole("button", { name: /export all/i }));

    await waitFor(() => expect(calendarApi.downloadUpcoming).toHaveBeenCalledTimes(1));
    expect(calendarApi.downloadAppointment).not.toHaveBeenCalled();
  });
});
