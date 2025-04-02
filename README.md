# Balancy for Unity

**LiveOps & Monetization Workspace for Games**

Balancy is a comprehensive LiveOps and monetization solution designed specifically for game developers. It provides tools for managing game economy, player data, and in-app purchases, helping you optimize revenue and player experience.

## Features

- **Game Economy Management**: Design, balance and adjust your in-game economy
- **LiveOps Tools**: Run events, promotions, and content updates without app submission
- **Data-Driven Decisions**: Analyze player behavior and revenue metrics
- **Monetization Optimization**: Test and improve your monetization strategy
- **Remote Configuration**: Update game parameters on-the-fly
- **Cross-Platform Support**: Works across all platforms supported by Unity

## Installation

### Via OpenUPM

The recommended way to install Balancy is through OpenUPM:

```
openupm add co.balancy.unity
```

### Via Git URL

You can also add this package through the Package Manager using Git URL:

1. Open Unity Package Manager
2. Click the "+" button
3. Select "Add package from git URL"
4. Enter: `https://github.com/balancy-liveops/plugin_cpp_unity.git`

## Getting Started

1. After installing the package, go to **Tools → Balancy → Config** to configure your project
2. Enter your API key (obtain it from [Balancy Dashboard](https://balancy.dev/))
3. Follow the integration [documentation](https://en.docs.balancy.dev/cpp/cpp_sdk/#unity) the SDK in your game. 

## Cheat Panel

The package includes a powerful CheatPanel for debugging and testing Balancy functionality. **We strongly recommend testing the CheatPanel** during your initial setup and familiarization with the package.

To open the CheatPanel:
1. Open scene at `Balancy/CheatPanel/BalancyCheatScene`
2. Alternatively, you can add the Cheat Panel prefab to your scene (located at `Balancy/CheatPanel/BalancyCheatPanel.prefab`)

The CheatPanel allows you to:
- Test and verify your configuration
- Monitor real-time data exchange
- Debug issues during implementation
- Explore available features through a visual interface
- Test various scenarios without modifying your code

**Note:** The CheatPanel utilizes TextMeshPro, which will be installed automatically as a dependency.

## Documentation

For detailed documentation and guides, visit:
- [Balancy Documentation](https://en.docs.balancy.dev)
- [Integration Guides](https://en.docs.balancy.dev/cpp/cpp_sdk/#unity)

## Support

If you encounter any issues or have questions:
- [GitHub Issues](https://github.com/balancy-liveops/plugin_cpp_unity/issues)
- Email: [contact@balancy.co](mailto:contact@balancy.co)
- [Discord Community](https://discord.gg/balancy) (replace with your actual Discord link)

## License

This package is licensed under the MIT License - see the LICENSE file for details.

## Requirements

- Unity 2022.3 or newer
- TextMeshPro package (automatically installed as dependency)