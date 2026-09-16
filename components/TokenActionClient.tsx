"use client";

import { authApi } from "@/lib/api/auth";
import TokenAction from "./TokenAction";

export default function TokenActionClient({ kind }: { kind: "magic" | "verify" }) {
  if (kind === "magic")
    return <TokenAction action={authApi.consumeMagic} pending="Prijavljujemo vas…" success="" />;
  return <TokenAction action={authApi.verifyEmail} pending="Potvrđujemo vaš e-mail…" success="" />;
}
