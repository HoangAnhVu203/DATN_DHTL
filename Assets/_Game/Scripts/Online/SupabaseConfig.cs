using UnityEngine;

[CreateAssetMenu(menuName = "Game/Supabase Config")]
public class SupabaseConfig : ScriptableObject
{
    public string SupabaseUrl;
    public string FunctionUrl;
    public string AnonKey;
}
