"use client";

import { useEffect } from "react";
import { useAuth } from "@/stores/auth";

/** Puni auth store pri prvom renderu (jedan /api/me). Bez UI-ja. */
export default function AuthBootstrap() {
  const bootstrap = useAuth((s) => s.bootstrap);
  useEffect(() => {
    void bootstrap();
  }, [bootstrap]);
  return null;
}
