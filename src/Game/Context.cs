using System.Collections.Generic;
using HarmonyLib;
using Rummage.Core;

namespace Rummage.Game;

/// <summary>What a grab is for and what it asks for.</summary>
internal sealed class Target
{
    public Target(string label, IReadOnlyList<Want> wants)
    {
        Label = label;
        Wants = wants;
    }

    public string Label { get; }

    public IReadOnlyList<Want> Wants { get; }
}

/// <summary>Works out what the grab key is for from what the player is looking at.</summary>
internal static class Context
{
    private static readonly AccessTools.FieldRef<Hud, Piece> HoveredPiece = AccessTools.FieldRefAccess<Hud, Piece>("m_hoveredPiece");

    /// <summary>The build piece under the cursor in the build menu, else the one being placed.</summary>
    public static Target Find(Player player)
    {
        Piece piece = null;
        if (Hud.IsPieceSelectionVisible() && Hud.instance)
        {
            piece = HoveredPiece(Hud.instance);
        }

        if (!piece && player.InPlaceMode())
        {
            piece = player.GetSelectedPiece();
        }

        return piece ? ForPiece(piece) : null;
    }

    /// <summary>What one of the piece costs, as a total to carry: what the inventory holds counts.</summary>
    public static Target ForPiece(Piece piece)
    {
        var wants = new List<Want>();
        foreach (var requirement in piece.m_resources ?? [])
        {
            var item = requirement?.m_resItem ? requirement.m_resItem.m_itemData : null;
            if (item?.m_shared != null && requirement.m_amount > 0)
            {
                wants.Add(new Want(item.m_shared.m_name, requirement.m_amount, countCarried: true, minWorldLevel: global::Game.m_worldLevel));
            }
        }

        string label = Localization.instance != null ? Localization.instance.Localize(piece.m_name) : piece.m_name;
        return new Target(label, wants);
    }
}
