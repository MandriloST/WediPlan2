import type { Metadata, Viewport } from "next";
import { SITE_URL } from "@/lib/site";
import "maplibre-gl/dist/maplibre-gl.css";
import "./globals.css";
import Providers from "./providers";
import Header from "@/components/Header";
import MobileTabBar from "@/components/MobileTabBar";
import CompareTray from "@/components/CompareTray";
import Toast from "@/components/Toast";
import BudgetDrawer from "@/components/BudgetDrawer";
import SWRegister from "@/components/SWRegister";
import Analytics from "@/components/Analytics";

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: "Wediplan — sve za vjenčanje u Hrvatskoj",
  description:
    "Pronađite restorane, fotografe, bendove i sve za vjenčanje u Hrvatskoj. Transparentne cijene, usporedba i kalkulator budžeta.",
  manifest: "/manifest.webmanifest",
  icons: { icon: "/icons/icon-192.png", apple: "/icons/icon-192.png" },
};

export const viewport: Viewport = {
  themeColor: "#2470cc",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="hr">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Fraunces:ital,opsz,wght@0,9..144,500..650;1,9..144,500..650&family=Instrument+Sans:wght@400;500;600;700&display=swap"
          rel="stylesheet"
        />
      </head>
      <body>
        <Providers>
          <Header />
          {children}
          <CompareTray />
          <BudgetDrawer />
          <Toast />
          <MobileTabBar />
          <SWRegister />
          <Analytics />
        </Providers>
      </body>
    </html>
  );
}
