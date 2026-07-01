using System.Text.Json;
using System.Text.Json.Nodes;
using WPT.Core.Models;

namespace WPT.Core.Services;

public static class SingBoxConfigBuilder
{
    public static string Build(ProxyProfile profile, int localPort)
    {
        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "warn" },
            ["inbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mixed",
                    ["tag"] = "mixed-in",
                    ["listen"] = "127.0.0.1",
                    ["listen_port"] = localPort
                }
            },
            ["outbounds"] = new JsonArray
            {
                BuildOutbound(profile),
                new JsonObject { ["type"] = "direct", ["tag"] = "direct" }
            },
            ["route"] = new JsonObject
            {
                ["final"] = "proxy",
                ["auto_detect_interface"] = true
            }
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static string BuildForProcessMode(ProxyProfile profile, int localPort)
    {
        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "warn" },
            ["dns"] = new JsonObject
            {
                ["servers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["tag"] = "local",
                        ["type"] = "local"
                    },
                    new JsonObject
                    {
                        ["tag"] = "remote",
                        ["type"] = "udp",
                        ["server"] = "8.8.8.8"
                    }
                },
                ["final"] = "remote"
            },
            ["inbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "socks",
                    ["tag"] = "socks-in",
                    ["listen"] = "127.0.0.1",
                    ["listen_port"] = localPort,
                    ["udp_timeout"] = "5m"
                }
            },
            ["outbounds"] = new JsonArray
            {
                BuildOutbound(profile),
                new JsonObject { ["type"] = "direct", ["tag"] = "direct" }
            },
            ["route"] = new JsonObject
            {
                ["final"] = "proxy",
                ["auto_detect_interface"] = false
            }
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject BuildOutbound(ProxyProfile profile) =>
        profile.Protocol switch
        {
            "vless" => BuildVlessOutbound(profile),
            "vmess" => BuildVmessOutbound(profile),
            "trojan" => BuildTrojanOutbound(profile),
            "ss" => BuildShadowsocksOutbound(profile),
            _ => throw new NotSupportedException($"Протокол {profile.Protocol} не поддерживается")
        };

    private static JsonObject BuildVlessOutbound(ProxyProfile profile)
    {
        var outbound = new JsonObject
        {
            ["type"] = "vless",
            ["tag"] = "proxy",
            ["server"] = profile.Server,
            ["server_port"] = profile.ServerPort,
            ["uuid"] = profile.Uuid
        };

        if (!string.IsNullOrWhiteSpace(profile.Flow))
        {
            outbound["flow"] = profile.Flow;
        }

        AppendTransport(outbound, profile);
        AppendTls(outbound, profile);
        return outbound;
    }

    private static JsonObject BuildVmessOutbound(ProxyProfile profile)
    {
        var outbound = new JsonObject
        {
            ["type"] = "vmess",
            ["tag"] = "proxy",
            ["server"] = profile.Server,
            ["server_port"] = profile.ServerPort,
            ["uuid"] = profile.Uuid,
            ["security"] = "auto"
        };

        AppendTransport(outbound, profile);
        AppendTls(outbound, profile);
        return outbound;
    }

    private static JsonObject BuildTrojanOutbound(ProxyProfile profile)
    {
        var outbound = new JsonObject
        {
            ["type"] = "trojan",
            ["tag"] = "proxy",
            ["server"] = profile.Server,
            ["server_port"] = profile.ServerPort,
            ["password"] = profile.Password
        };

        AppendTransport(outbound, profile);
        AppendTls(outbound, profile);
        return outbound;
    }

    private static JsonObject BuildShadowsocksOutbound(ProxyProfile profile) =>
        new()
        {
            ["type"] = "shadowsocks",
            ["tag"] = "proxy",
            ["server"] = profile.Server,
            ["server_port"] = profile.ServerPort,
            ["method"] = profile.Method,
            ["password"] = profile.Password
        };

    private static void AppendTransport(JsonObject outbound, ProxyProfile profile)
    {
        var transportType = profile.Transport.ToLowerInvariant();
        switch (transportType)
        {
            case "ws":
            case "websocket":
                outbound["transport"] = new JsonObject
                {
                    ["type"] = "ws",
                    ["path"] = string.IsNullOrWhiteSpace(profile.Path) ? "/" : profile.Path,
                    ["headers"] = string.IsNullOrWhiteSpace(profile.Host)
                        ? null
                        : new JsonObject { ["Host"] = profile.Host }
                };
                break;

            case "grpc":
                outbound["transport"] = new JsonObject
                {
                    ["type"] = "grpc",
                    ["service_name"] = profile.ServiceName ?? string.Empty
                };
                break;

            case "http":
            case "h2":
                outbound["transport"] = new JsonObject
                {
                    ["type"] = "http",
                    ["host"] = string.IsNullOrWhiteSpace(profile.Host)
                        ? new JsonArray()
                        : new JsonArray(profile.Host),
                    ["path"] = profile.Path ?? "/"
                };
                break;

            case "quic":
                outbound["transport"] = new JsonObject { ["type"] = "quic" };
                break;
        }
    }

    private static void AppendTls(JsonObject outbound, ProxyProfile profile)
    {
        var security = profile.Security.ToLowerInvariant();
        if (security is "none" or "")
        {
            return;
        }

        var tls = new JsonObject { ["enabled"] = true };

        if (!string.IsNullOrWhiteSpace(profile.Sni))
        {
            tls["server_name"] = profile.Sni;
        }

        if (profile.AllowInsecure)
        {
            tls["insecure"] = true;
        }

        if (!string.IsNullOrWhiteSpace(profile.Alpn))
        {
            var alpn = new JsonArray();
            foreach (var item in profile.Alpn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                alpn.Add(item);
            }

            tls["alpn"] = alpn;
        }

        if (!string.IsNullOrWhiteSpace(profile.Fingerprint))
        {
            tls["utls"] = new JsonObject
            {
                ["enabled"] = true,
                ["fingerprint"] = profile.Fingerprint
            };
        }

        if (security is "reality")
        {
            tls["reality"] = new JsonObject
            {
                ["enabled"] = true,
                ["public_key"] = profile.PublicKey ?? string.Empty,
                ["short_id"] = profile.ShortId ?? string.Empty
            };
        }

        outbound["tls"] = tls;
    }
}
