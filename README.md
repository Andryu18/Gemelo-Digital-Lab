# Gemelo digital de la dirección de un vehículo (situación 3)

Un potenciómetro y la escena de Unity controlan el mismo servomotor, y la escena muestra el vehículo girando y avanzando por un circuito.

## Contenido
- `Firmware/SteeringFirmware/SteeringFirmware.ino`: firmware del Arduino Uno.
- `Assets/`, `Packages/`, `ProjectSettings/`: proyecto de Unity 6000.3.8f1 (URP).
- `Modelado/CarroGemelo.blend`: modelo 3D del carro, hecho en Blender.

## Montaje
- Potenciómetro: patas extremas a 5 V y GND, pata central a `A0`.
- Servomotor SG90: señal al pin 9, alimentado con 4 pilas AA (4.8 a 6 V) y GND común con el Arduino.

## Cómo ejecutarlo
1. Abrir `SteeringFirmware.ino` en el IDE de Arduino y cargarlo en el Arduino Uno. Cerrar el monitor serial.
2. Abrir el proyecto en Unity 6000.3.8f1 y la escena `Assets/Scenes/SampleScene`.
3. En `Edit > Project Settings > Player > Other Settings`, poner `Api Compatibility Level` en `.NET Framework`.
4. En el objeto `Serial_Bridge`, poner el puerto COM del Arduino (en la escena está en `COM5`).
5. Pulsar Play. La tecla `R` reinicia el vehículo.

## Protocolo serial (texto, 9600 baudios, terminado en `\n`)
| Sentido | Formato | Función |
|---|---|---|
| Unity a Arduino | `A:<ángulo>` | Comando digital, entero de 0 a 180 (se limita a 45 a 135) |
| Arduino a Unity | `S:<ángulo>,<fuente>` | Estado cada 50 ms, fuente `POT` o `DIG` |

## Arbitraje
Tras un comando `A:` el potenciómetro queda suelto (LED encendido) hasta que pasa por el ángulo ordenado. Luego retoma el control sin saltos.
