"use client";

import { useEffect } from "react";
import { useCompare } from "@/stores";

export default function Toast() {
  const { toast, clearToast } = useCompare();
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(clearToast, 3500);
    return () => clearTimeout(t);
  }, [toast, clearToast]);
  if (!toast) return null;
  return (
    <div className="toast" role="alert">
      {toast}
    </div>
  );
}
