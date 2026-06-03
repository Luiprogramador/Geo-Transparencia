using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public static class SupabaseRestClient
{
    public static IEnumerator Get(string baseRestUrl, string publishableKey, string resourcePath, Action<long, string, string> onCompleted)
    {
        string url = baseRestUrl.TrimEnd('/') + "/" + resourcePath.TrimStart('/');
        Debug.Log($"[Supabase] GET: {url}");

        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("apikey", publishableKey);
        req.SetRequestHeader("Authorization", "Bearer " + publishableKey);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Supabase] Erro: {req.error} (HTTP {req.responseCode})");
            onCompleted?.Invoke(req.responseCode, "", req.error);
        }
        else
        {
            Debug.Log($"[Supabase] Resposta recebida ({req.downloadHandler.text.Length} bytes)");
            onCompleted?.Invoke(req.responseCode, req.downloadHandler.text, "");
        }
    }
}