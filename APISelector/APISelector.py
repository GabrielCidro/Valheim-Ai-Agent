# %%
import os
import subprocess
import tkinter as tk
from tkinter import simpledialog, messagebox

CFG_PATH = r"C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\config\aedenthorn.ValAI.cfg"

def update_api_key(api_key: str):
    if not os.path.exists(CFG_PATH):
        messagebox.showerror("Erro", f"Arquivo não encontrado:\n{CFG_PATH}")
        return False

    with open(CFG_PATH, "r", encoding="utf-8") as f:
        lines = f.readlines()

    new_lines = []
    inside_openai_section = False
    updated = False

    for line in lines:
        stripped = line.strip()

        # Detecta quando entra na seção [OpenAI]
        if stripped.startswith("[OpenAI]"):
            inside_openai_section = True
            new_lines.append(line)
            continue

        # Sai da seção se encontrar outra seção
        if inside_openai_section and stripped.startswith("[") and stripped.endswith("]"):
            inside_openai_section = False

        # Atualiza a chave dentro da seção
        if inside_openai_section and stripped.startswith("ApiKey"):
            new_lines.append(f"ApiKey = {api_key}\n")
            updated = True
        else:
            new_lines.append(line)

    # Caso a chave não exista, adiciona dentro da seção [OpenAI]
    if inside_openai_section and not updated:
        new_lines.append(f"ApiKey = {api_key}\n")

    with open(CFG_PATH, "w", encoding="utf-8") as f:
        f.writelines(new_lines)

    return True


def launch_valheim():
    """Abre o Valheim pelo Steam (padrão)"""
    try:
        # Caminho padrão do executável (pode variar conforme instalação)
        valheim_path = r"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim.exe"

        if os.path.exists(valheim_path):
            subprocess.Popen([valheim_path])
        else:
            # Se não encontrar, tenta abrir via SteamID do Valheim
            subprocess.Popen(["steam://rungameid/892970"], shell=True)

        messagebox.showinfo("Jogo iniciado", "Valheim está sendo iniciado...")
    except Exception as e:
        messagebox.showerror("Erro ao iniciar o jogo", str(e))


def main():
    root = tk.Tk()
    root.withdraw()

    ai_choice = simpledialog.askstring("Escolher IA", "Digite qual IA deseja usar (ex: GPT-4, Claude, Gemini):")
    if not ai_choice:
        messagebox.showinfo("Cancelado", "Nenhuma IA selecionada.")
        return

    api_key = simpledialog.askstring("Chave da API", f"Insira a chave da OpenAI para {ai_choice}:")
    if not api_key:
        messagebox.showinfo("Cancelado", "Nenhuma chave informada.")
        return

    if update_api_key(api_key):
        messagebox.showinfo("Sucesso", f"Chave da OpenAI salva com sucesso em:\n{CFG_PATH}")
        launch_valheim()
    else:
        messagebox.showerror("Erro", "Falha ao atualizar o arquivo de configuração.")


if __name__ == "__main__":
    main()

# %%
