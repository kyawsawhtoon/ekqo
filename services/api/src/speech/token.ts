/**
 * Issues a short-lived Azure AI Speech token (valid 10 minutes) so the browser
 * can talk to Azure directly without ever seeing the subscription key.
 */
export interface SpeechConfig { key?: string; region: string }

export async function issueSpeechToken(cfg: SpeechConfig, fetchImpl: typeof fetch = fetch) {
  if (!cfg.key) throw new Error("AZURE_SPEECH_KEY is not set");
  const res = await fetchImpl(`https://${cfg.region}.api.cognitive.microsoft.com/sts/v1.0/issueToken`, {
    method: "POST",
    headers: { "Ocp-Apim-Subscription-Key": cfg.key, "Content-Length": "0" },
  });
  if (!res.ok) throw new Error(`Azure token request failed: ${res.status}`);
  return { token: await res.text(), region: cfg.region, expiresInSeconds: 540 };
}
