# apps/ai/scripts/monitor_training.py
import matplotlib.pyplot as plt
import pandas as pd
from pathlib import Path

def plot_training_metrics(results_path='runs/detect/sssp_v1'):
    """
    Visualize training metrics
    """
    results_file = Path(results_path) / 'results.csv'
    
    if not results_file.exists():
        print("No results file found. Training might still be running.")
        return
    
    # Load results
    df = pd.read_csv(results_file)
    df.columns = df.columns.str.strip()  # Remove whitespace
    
    # Create subplots
    fig, axes = plt.subplots(2, 2, figsize=(15, 10))
    
    # Loss plots
    axes[0, 0].plot(df['epoch'], df['train/box_loss'], label='Box Loss')
    axes[0, 0].plot(df['epoch'], df['train/cls_loss'], label='Class Loss')
    axes[0, 0].set_title('Training Loss')
    axes[0, 0].legend()
    
    # mAP plots
    axes[0, 1].plot(df['epoch'], df['metrics/mAP50(B)'], label='mAP@0.5')
    axes[0, 1].plot(df['epoch'], df['metrics/mAP50-95(B)'], label='mAP@0.5:0.95')
    axes[0, 1].set_title('Mean Average Precision')
    axes[0, 1].legend()
    
    # Precision & Recall
    axes[1, 0].plot(df['epoch'], df['metrics/precision(B)'], label='Precision')
    axes[1, 0].plot(df['epoch'], df['metrics/recall(B)'], label='Recall')
    axes[1, 0].set_title('Precision & Recall')
    axes[1, 0].legend()
    
    # Learning Rate
    axes[1, 1].plot(df['epoch'], df['lr/pg0'])
    axes[1, 1].set_title('Learning Rate')
    
    plt.tight_layout()
    plt.savefig(f'{results_path}/training_metrics.png')
    print(f"Metrics saved to: {results_path}/training_metrics.png")

if __name__ == "__main__":
    plot_training_metrics()